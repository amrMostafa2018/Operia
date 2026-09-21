using System.Data;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Commands.CancelBooking;
using Operia.Application.Bookings.Commands.CreateBooking;
using Operia.Application.Common.Exceptions;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;

namespace Operia.Infrastructure.Persistence;

/// <summary>Serializes booking mutations and performs the final slot and session availability checks.</summary>
public sealed class BookingConcurrencyStore(ApplicationDbContext db) : IBookingConcurrencyStore, IBookingCancellationStore
{
    /// <summary>Runs cancellation under serializable slot and purchase locks before committing its changes.</summary>
    public async Task<CancelBookingResult> ExecuteAsync(
        string tenantId,
        string employeeId,
        DateOnly scheduledDate,
        IReadOnlyList<string> customerPackageIds,
        Func<CancellationToken, Task<CancelBookingResult>> cancel,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
        {
            return await cancel(cancellationToken);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await AcquireLockAsync(
            $"booking-slot:{tenantId}:{employeeId}:{scheduledDate:yyyyMMdd}",
            cancellationToken);
        foreach (var customerPackageId in customerPackageIds.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
        {
            await AcquireLockAsync($"booking-package:{tenantId}:{customerPackageId}", cancellationToken);
        }

        var result = await cancel(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>Locks the idempotency key, slot, and existing purchases before the final create checks and save.</summary>
    public async Task<Booking> CommitCreateAsync(
        Booking booking,
        IReadOnlyList<BookingPackageReservation> reservations,
        IReadOnlyList<CustomerPackage> newPurchases,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
        {
            return await CommitCoreAsync(booking, reservations, newPurchases, utcNow, cancellationToken);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        await AcquireLockAsync(
            $"booking-idempotency:{booking.TenantId}:{booking.IdempotencyKey}",
            cancellationToken);
        var existing = await db.Bookings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == booking.TenantId && x.IdempotencyKey == booking.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            DetachPendingChanges();
            await transaction.RollbackAsync(cancellationToken);
            return existing;
        }

        await AcquireLockAsync(
            $"booking-slot:{booking.TenantId}:{booking.EmployeeId}:{booking.ScheduledDate:yyyyMMdd}",
            cancellationToken);
        var newPurchaseIds = newPurchases.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var customerPackageId in reservations
                     .Select(x => x.CustomerPackageId)
                     .Where(x => !newPurchaseIds.Contains(x))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(x => x, StringComparer.Ordinal))
        {
            await AcquireLockAsync(
                $"booking-package:{booking.TenantId}:{customerPackageId}",
                cancellationToken);
        }

        var saved = await CommitCoreAsync(booking, reservations, newPurchases, utcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return saved;
    }

    /// <summary>Rejects overlapping bookings or holds and reserves the next free session for each purchase.</summary>
    private async Task<Booking> CommitCoreAsync(
        Booking booking,
        IReadOnlyList<BookingPackageReservation> reservations,
        IReadOnlyList<CustomerPackage> newPurchases,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var bookingConflict = await db.Bookings.AsNoTracking().AnyAsync(
            x => x.TenantId == booking.TenantId &&
                 x.EmployeeId == booking.EmployeeId &&
                 x.ScheduledDate == booking.ScheduledDate &&
                 x.Status != BookingStatus.Cancelled &&
                 booking.StartMinutes < x.EndMinutes &&
                 booking.EndMinutes > x.StartMinutes,
            cancellationToken);
        var holdConflict = await db.BookingHolds.AsNoTracking().AnyAsync(
            x => x.TenantId == booking.TenantId &&
                 x.EmployeeId == booking.EmployeeId &&
                 x.ScheduledDate == booking.ScheduledDate &&
                 x.ConvertedBookingId == null &&
                 x.ExpiresAtUtc > utcNow &&
                 booking.StartMinutes < x.EndMinutes &&
                 booking.EndMinutes > x.StartMinutes,
            cancellationToken);
        if (bookingConflict || holdConflict)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.SlotUnavailable, "scheduledDate");
        }

        var newPurchaseById = newPurchases.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var loadedPackages = new Dictionary<string, CustomerPackage>(StringComparer.Ordinal);
        foreach (var reservation in reservations)
        {
            if (!newPurchaseById.TryGetValue(reservation.CustomerPackageId, out var owned))
            {
                if (!loadedPackages.TryGetValue(reservation.CustomerPackageId, out owned))
                {
                    owned = await db.CustomerPackages.SingleOrDefaultAsync(
                        x => x.TenantId == booking.TenantId &&
                             x.Id == reservation.CustomerPackageId &&
                             x.CustomerId == booking.CustomerId,
                        cancellationToken);
                    if (owned is not null)
                    {
                        loadedPackages.Add(owned.Id, owned);
                    }
                }
            }
            if (owned is null || !owned.IsActive ||
                (owned.ExpiresOn is { } expiresOn && expiresOn < DateOnly.FromDateTime(utcNow)) ||
                owned.TotalSessions <= owned.UsedSessions + owned.ReservedSessions)
            {
                throw ConflictException.FromCode(ApiErrorCodes.Bookings.PackageSessionUnavailable, "items");
            }

            var occupied = await db.BookingPackageReservations.AsNoTracking()
                .Where(x => x.TenantId == booking.TenantId &&
                            x.CustomerPackageId == owned.Id &&
                            x.ReleasedAtUtc == null)
                .Select(x => x.SessionNumber)
                .ToListAsync(cancellationToken);
            occupied.AddRange(reservations
                .Where(x => x.CustomerPackageId == owned.Id && x.SessionNumber > 0)
                .Select(x => x.SessionNumber));
            var taken = occupied.ToHashSet();
            var next = Enumerable.Range(owned.UsedSessions + 1, owned.TotalSessions - owned.UsedSessions)
                .FirstOrDefault(x => !taken.Contains(x));
            if (next == 0)
            {
                throw ConflictException.FromCode(ApiErrorCodes.Bookings.PackageSessionUnavailable, "items");
            }

            reservation.SessionNumber = next;
            owned.ReservedSessions += 1;
        }

        await db.SaveChangesAsync(cancellationToken);
        return booking;
    }

    /// <summary>Acquires the database application lock shared by booking mutations.</summary>
    private Task AcquireLockAsync(string resource, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC dbo.AcquireBookingMutationLock @Resource={resource}",
            cancellationToken);

    /// <summary>Discards staged create records when the same idempotency key already committed.</summary>
    private void DetachPendingChanges()
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(x => x.State == EntityState.Added))
        {
            entry.State = EntityState.Detached;
        }
    }
}
