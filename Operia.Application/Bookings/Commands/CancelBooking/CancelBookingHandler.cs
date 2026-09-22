using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Bookings.Commands.CancelBooking;

/// <summary>
/// Cancels an eligible Booked appointment and returns unused Package and standalone service sessions.
/// </summary>
public sealed class CancelBookingHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IAuditWriter auditWriter,
    IBookingCancellationStore cancellationStore,
    IDateTimeProvider clock) : IRequestHandler<CancelBookingCommand, CancelBookingResult>
{
    /// <summary>Checks access and state, then acquires the locks needed for an atomic cancellation.</summary>
    public async Task<CancelBookingResult> Handle(CancelBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        var booking = await db.Bookings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.BookingId, cancellationToken)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Bookings.NotFound);
        var allowedBranches = await BookingAccess.GetAllowedBranchesAsync(currentUser, branchScope, cancellationToken);
        BookingAccess.RequireBranch(allowedBranches, booking.BranchId);
        if (IsAlreadyCancelled(booking))
        {
            return ToResult(booking);
        }

        var version = ParseVersion(request.Version);
        var packageIds = await db.BookingPackageReservations.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BookingId == booking.Id && x.CancellationAtUtc == null)
            .Select(x => x.CustomerPackageId)
            .ToListAsync(cancellationToken);

        return await cancellationStore.ExecuteAsync(
            tenantId,
            booking.EmployeeId,
            booking.ScheduledDate,
            packageIds,
            token => CancelUnderLockAsync(request, tenantId, allowedBranches, version, token),
            cancellationToken);
    }

    /// <summary>Rechecks state and version under the cancellation lock before releasing balances.</summary>
    private async Task<CancelBookingResult> CancelUnderLockAsync(
        CancelBookingCommand request,
        string tenantId,
        IReadOnlyCollection<string> allowedBranches,
        byte[] version,
        CancellationToken cancellationToken)
    {
        var booking = await db.Bookings
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.BookingId, cancellationToken)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Bookings.NotFound);
        BookingAccess.RequireBranch(allowedBranches, booking.BranchId);
        if (IsAlreadyCancelled(booking))
        {
            return ToResult(booking);
        }
        if (!booking.Version.SequenceEqual(version))
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.Changed);
        }

        var releasedSessions = await ReleaseReservationsAsync(tenantId, booking, cancellationToken);
        StageCancellation(tenantId, booking, releasedSessions);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.Changed);
        }

        return ToResult(booking);
    }

    /// <summary>Treats a repeated cancellation as success and rejects other terminal states.</summary>
    private static bool IsAlreadyCancelled(Booking booking)
    {
        if (booking.Status == BookingStatus.Cancelled)
        {
            return true;
        }
        if (booking.Status != BookingStatus.Booked)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.TerminalReadOnly);
        }
        return false;
    }

    /// <summary>Converts the client row version to bytes or reports a stale booking.</summary>
    private static byte[] ParseVersion(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.Changed);
        }
    }

    /// <summary>Returns active reservations and restores paid standalone units missing from older records.</summary>
    private async Task<List<ReleasedSession>> ReleaseReservationsAsync(
        string tenantId,
        Booking booking,
        CancellationToken cancellationToken)
    {
        var reservations = await db.BookingPackageReservations
            .Where(x => x.TenantId == tenantId && x.BookingId == booking.Id && x.CancellationAtUtc == null)
            .ToListAsync(cancellationToken);
        var standaloneUnits = await db.BookingItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                        x.BookingId == booking.Id &&
                        x.Type != BookingItemType.PackageSession &&
                        x.PackageId != null)
            .GroupBy(x => x.PackageId!)
            .Select(group => new { PackageId = group.Key, Quantity = group.Sum(x => x.Quantity) })
            .ToListAsync(cancellationToken);
        var releasedServiceCounts = standaloneUnits.ToDictionary(x => x.PackageId, _ => 0, StringComparer.Ordinal);
        var releasedSessions = reservations
            .Select(x => new ReleasedSession(x.CustomerPackageId, x.SessionNumber))
            .ToList();
        await ReleaseActiveReservationsAsync(tenantId, booking, reservations, releasedServiceCounts, cancellationToken);

        // Bookings saved before standalone reservations existed still return their paid service units.
        foreach (var service in standaloneUnits)
        {
            for (var unit = releasedServiceCounts[service.PackageId]; unit < service.Quantity; unit++)
            {
                var purchase = CreateReturnedServicePurchase(tenantId, booking, service.PackageId);
                db.CustomerPackages.Add(purchase);
                db.BookingPackageReservations.Add(new BookingPackageReservation
                {
                    TenantId = tenantId,
                    BookingId = booking.Id,
                    CustomerPackageId = purchase.Id,
                    SessionNumber = 1,
                    CancellationAtUtc = clock.UtcNow
                });
                releasedSessions.Add(new ReleasedSession(purchase.Id, 1));
            }
        }
        return releasedSessions;
    }

    /// <summary>Marks each active reservation released and decrements its owned balance.</summary>
    private async Task ReleaseActiveReservationsAsync(
        string tenantId,
        Booking booking,
        IReadOnlyList<BookingPackageReservation> reservations,
        Dictionary<string, int> releasedServiceCounts,
        CancellationToken cancellationToken)
    {
        foreach (var reservation in reservations)
        {
            var owned = await db.CustomerPackages.SingleOrDefaultAsync(
                x => x.TenantId == tenantId &&
                     x.Id == reservation.CustomerPackageId &&
                     x.CustomerId == booking.CustomerId,
                cancellationToken);
            if (owned is null || owned.ReservedSessionCount < 1)
            {
                throw ConflictException.FromCode(ApiErrorCodes.Bookings.Changed);
            }

            reservation.CancellationAtUtc = clock.UtcNow;
            owned.ReservedSessions = owned.ReservedSessionCount - 1;
            if (releasedServiceCounts.ContainsKey(owned.PackageId))
            {
                releasedServiceCounts[owned.PackageId] += 1;
            }
        }
    }

    /// <summary>Creates the one-session balance returned for a legacy standalone service unit.</summary>
    private static CustomerPackage CreateReturnedServicePurchase(string tenantId, Booking booking, string packageId)
    {
        return new CustomerPackage
        {
            TenantId = tenantId,
            CustomerId = booking.CustomerId,
            PackageId = packageId,
            Total = 1,
            ReservedSessions = 0,
            IsActive = true
        };
    }

    /// <summary>Stages the status change, booking history, and audit record for one save.</summary>
    private void StageCancellation(string tenantId, Booking booking, IReadOnlyList<ReleasedSession> releasedSessions)
    {
        booking.Status = BookingStatus.Cancelled;
        db.BookingHistory.Add(new BookingHistory
        {
            TenantId = tenantId,
            BookingId = booking.Id,
            Action = "Cancelled",
            ChangedByUserId = currentUser.UserId,
            ChangedByDisplayName = currentUser.DisplayName,
            ChangesJson = JsonSerializer.Serialize(new
            {
                OldStatus = BookingStatus.Booked,
                NewStatus = BookingStatus.Cancelled,
                ReleasedSessions = releasedSessions
            })
        });
        auditWriter.Write(
            tenantId,
            AuditActions.BookingCancelled,
            nameof(Booking),
            booking.Id,
            booking.BookingNumber,
            new
            {
                OldStatus = BookingStatus.Booked,
                NewStatus = BookingStatus.Cancelled,
                ReleasedSessions = releasedSessions
            });
    }

    /// <summary>Identifies a session balance returned by cancellation history.</summary>
    private sealed record ReleasedSession(string CustomerPackageId, int SessionNumber);

    /// <summary>Returns the booking identity, status, and current concurrency version.</summary>
    private static CancelBookingResult ToResult(Booking booking) => new(booking.Id, booking.Status.ToString(), Convert.ToBase64String(booking.Version));
}
