using System.Text.Json;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Commands.CancelBooking;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;
using ValidationException = Operia.Application.Common.Exceptions.ValidationException;

namespace Operia.Application.Bookings.Commands.CloseBooking;

/// <summary>
/// Closes a booked appointment, finalises per-line package usage, and marks the booking completed.
/// </summary>
public sealed class CloseBookingHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IAuditWriter auditWriter,
    IBookingCancellationStore cancellationStore,
    IDateTimeProvider clock) : IRequestHandler<CloseBookingCommand, CloseBookingResult>
{
    /// <summary>Checks access and state, then acquires the locks needed for an atomic close.</summary>
    public async Task<CloseBookingResult> Handle(CloseBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        var booking = await db.Bookings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.BookingId, cancellationToken)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Bookings.NotFound);
        var allowedBranches = await BookingAccess.GetAllowedBranchesAsync(currentUser, branchScope, cancellationToken);
        BookingAccess.RequireBranch(allowedBranches, booking.BranchId);
        EnsureBooked(booking);

        var version = ParseVersion(request.Version);
        var packageIds = await db.BookingPackageReservations.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BookingId == booking.Id && x.CancellationAtUtc == null)
            .Select(x => x.CustomerPackageId)
            .ToListAsync(cancellationToken);

        var locked = await cancellationStore.ExecuteAsync(
            tenantId,
            booking.EmployeeId,
            booking.ScheduledDate,
            packageIds,
            token => CloseUnderLockAsync(request, tenantId, allowedBranches, version, token),
            cancellationToken);

        return new CloseBookingResult(locked.Id, locked.Status, locked.Version);
    }

    /// <summary>Rechecks state and version under the mutation lock before finalising balances.</summary>
    private async Task<CancelBookingResult> CloseUnderLockAsync(
        CloseBookingCommand request,
        string tenantId,
        IReadOnlyCollection<string> allowedBranches,
        byte[] version,
        CancellationToken cancellationToken)
    {
        var booking = await db.Bookings
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.BookingId, cancellationToken)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Bookings.NotFound);
        BookingAccess.RequireBranch(allowedBranches, booking.BranchId);
        EnsureBooked(booking);
        if (!booking.Version.SequenceEqual(version))
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.Changed);
        }

        var bookingItems = await db.BookingItems
            .Include(x => x.Package)
            .Where(x => x.TenantId == tenantId && x.BookingId == booking.Id)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        ValidateCloseItems(bookingItems, request.Items);

        var activeReservations = await db.BookingPackageReservations
            .Where(x => x.TenantId == tenantId && x.BookingId == booking.Id && x.CancellationAtUtc == null)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.CustomerPackageId)
            .ToListAsync(cancellationToken);

        var reservationQueues = await BuildReservationQueuesAsync(
            activeReservations,
            tenantId,
            booking.CustomerId,
            cancellationToken);
        var ownedPackages = new Dictionary<string, CustomerPackage>(StringComparer.Ordinal);
        var outcomes = new List<CloseOutcome>();

        foreach (var item in bookingItems)
        {
            var input = request.Items.Single(x => x.BookingItemId == item.Id);
            var reservations = ResolveReservations(
                item,
                input,
                activeReservations,
                reservationQueues);
            var isComplete = string.Equals(input.Status, "complete", StringComparison.OrdinalIgnoreCase);
            var pulseApplied = false;

            foreach (var reservation in reservations)
            {
                var owned = await LoadCustomerPackageAsync(
                    tenantId,
                    booking.CustomerId,
                    reservation.CustomerPackageId,
                    ownedPackages,
                    cancellationToken);
                var usesPulses = CustomerPackageBalance.UsesPulses(
                    owned.Package?.OfferType ?? OfferType.SingleSession,
                    owned.Package?.PulseCount);

                if (isComplete)
                {
                    if (usesPulses)
                    {
                        if (!pulseApplied)
                        {
                            ApplyPulseUsage(input, owned);
                            pulseApplied = true;
                        }

                        DecrementReservedSessions(owned);
                    }
                    else
                    {
                        owned.Used += 1;
                        DecrementReservedSessions(owned);
                    }
                }
                else
                {
                    reservation.CancellationAtUtc = clock.UtcNow;
                    DecrementReservedSessions(owned);
                }
            }

            outcomes.Add(new CloseOutcome(
                item.Id,
                FirstName(item.Name, item.Package?.Name),
                input.Status,
                input.PulsesUsed,
                input.Notes));
        }

        booking.Status = BookingStatus.Completed;
        StageCloseHistory(tenantId, booking, outcomes);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.Changed);
        }

        return new CancelBookingResult(
            booking.Id,
            booking.Status.ToString(),
            Convert.ToBase64String(booking.Version));
    }

    /// <summary>Rejects close when the booking is not in Booked status.</summary>
    private static void EnsureBooked(Booking booking)
    {
        if (booking.Status == BookingStatus.Booked)
        {
            return;
        }

        if (booking.Status is BookingStatus.Completed or BookingStatus.Cancelled)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.TerminalReadOnly);
        }

        throw ConflictException.FromCode(ApiErrorCodes.Bookings.NotBookedStatus);
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

    /// <summary>Requires the close payload to include every booking line exactly once.</summary>
    private static void ValidateCloseItems(
        IReadOnlyList<BookingItem> bookingItems,
        IReadOnlyList<CloseBookingItemInput> submittedItems)
    {
        if (bookingItems.Count != submittedItems.Count)
        {
            throw Invalid(ApiErrorCodes.Bookings.CloseItemsIncomplete, "items");
        }

        var bookingItemIds = bookingItems.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var submittedIds = submittedItems.Select(x => x.BookingItemId).ToHashSet(StringComparer.Ordinal);
        if (!bookingItemIds.SetEquals(submittedIds))
        {
            throw Invalid(ApiErrorCodes.Bookings.CloseItemsIncomplete, "items");
        }
    }

    /// <summary>Builds reservation queues keyed by package id in calendar enrichment order.</summary>
    private async Task<Dictionary<string, Queue<BookingPackageReservation>>> BuildReservationQueuesAsync(
        IReadOnlyList<BookingPackageReservation> activeReservations,
        string tenantId,
        string customerId,
        CancellationToken cancellationToken)
    {
        var packageIds = activeReservations
            .Select(x => x.CustomerPackageId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (packageIds.Count == 0)
        {
            return new Dictionary<string, Queue<BookingPackageReservation>>(StringComparer.Ordinal);
        }

        var packageIdByCustomerPackage = await db.CustomerPackages.AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                        x.CustomerId == customerId &&
                        packageIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.PackageId, StringComparer.Ordinal, cancellationToken);

        var queues = new Dictionary<string, Queue<BookingPackageReservation>>(StringComparer.Ordinal);
        foreach (var reservation in activeReservations)
        {
            if (!packageIdByCustomerPackage.TryGetValue(reservation.CustomerPackageId, out var packageId))
            {
                continue;
            }

            if (!queues.TryGetValue(packageId, out var queue))
            {
                queue = new Queue<BookingPackageReservation>();
                queues[packageId] = queue;
            }

            queue.Enqueue(reservation);
        }

        return queues;
    }

    /// <summary>Resolves active reservations tied to one submitted close line.</summary>
    private static List<BookingPackageReservation> ResolveReservations(
        BookingItem item,
        CloseBookingItemInput input,
        IReadOnlyList<BookingPackageReservation> activeReservations,
        IReadOnlyDictionary<string, Queue<BookingPackageReservation>> reservationQueues)
    {
        if (!string.IsNullOrWhiteSpace(input.CustomerPackageId))
        {
            var matched = activeReservations
                .Where(x => string.Equals(x.CustomerPackageId, input.CustomerPackageId, StringComparison.Ordinal))
                .ToList();
            if (matched.Count > 0)
            {
                return matched;
            }
        }

        if (item.PackageId is null || !reservationQueues.TryGetValue(item.PackageId, out var queue))
        {
            return [];
        }

        var resolved = new List<BookingPackageReservation>();
        for (var unit = 0; unit < item.Quantity && queue.Count > 0; unit++)
        {
            resolved.Add(queue.Dequeue());
        }

        return resolved;
    }

    /// <summary>Loads a tracked customer purchase with its package master for balance rules.</summary>
    private async Task<CustomerPackage> LoadCustomerPackageAsync(
        string tenantId,
        string customerId,
        string customerPackageId,
        Dictionary<string, CustomerPackage> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(customerPackageId, out var cached))
        {
            return cached;
        }

        var owned = await db.CustomerPackages
            .Include(x => x.Package)
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId &&
                     x.Id == customerPackageId &&
                     x.CustomerId == customerId,
                cancellationToken)
            ?? throw ConflictException.FromCode(ApiErrorCodes.Bookings.Changed);

        cache[customerPackageId] = owned;
        return owned;
    }

    /// <summary>Consumes pulse balance for one completed pulse-package line.</summary>
    private static void ApplyPulseUsage(CloseBookingItemInput input, CustomerPackage owned)
    {
        if (input.PulsesUsed is not > 0)
        {
            throw Invalid(ApiErrorCodes.Bookings.PulsesExceedRemaining, "items");
        }

        var remaining = CustomerPackageBalance.Remaining(
            owned.Total,
            owned.Used,
            owned.ReservedSessions,
            owned.Package?.OfferType ?? OfferType.SingleSession,
            owned.Package?.PulseCount);
        if (input.PulsesUsed.Value > remaining)
        {
            throw Invalid(ApiErrorCodes.Bookings.PulsesExceedRemaining, "items");
        }

        owned.Used += input.PulsesUsed.Value;
    }

    /// <summary>Decrements one reserved session slot when a reservation is finalised.</summary>
    private static void DecrementReservedSessions(CustomerPackage owned)
    {
        if (owned.ReservedSessionCount > 0)
        {
            owned.ReservedSessions = owned.ReservedSessionCount - 1;
        }
    }

    /// <summary>Stages booking history and audit for a successful close.</summary>
    private void StageCloseHistory(
        string tenantId,
        Booking booking,
        IReadOnlyList<CloseOutcome> outcomes)
    {
        var payload = new
        {
            OldStatus = BookingStatus.Booked,
            NewStatus = BookingStatus.Completed,
            Items = outcomes.Select(x => new
            {
                BookingItemId = x.BookingItemId,
                Name = x.Name,
                Status = x.Status,
                PulsesUsed = x.PulsesUsed,
                Notes = x.Notes
            })
        };

        db.BookingHistory.Add(new BookingHistory
        {
            TenantId = tenantId,
            BookingId = booking.Id,
            Action = "Closed",
            ChangedByUserId = currentUser.UserId,
            ChangedByDisplayName = currentUser.DisplayName,
            ChangesJson = JsonSerializer.Serialize(payload)
        });
        auditWriter.Write(
            tenantId,
            AuditActions.BookingClosed,
            nameof(Booking),
            booking.Id,
            booking.BookingNumber,
            payload);
    }

    /// <summary>Builds a coded field validation failure for close input.</summary>
    private static ValidationException Invalid(string code, string field) =>
        new([new ValidationFailure(field, code) { ErrorCode = code }]);

    /// <summary>Uses the saved line name, then the catalog package name.</summary>
    private static string FirstName(string? itemName, string? packageName) =>
        !string.IsNullOrWhiteSpace(itemName) ? itemName.Trim() :
        !string.IsNullOrWhiteSpace(packageName) ? packageName.Trim() :
        string.Empty;

    /// <summary>Captures one submitted close line for history and audit payloads.</summary>
    private sealed record CloseOutcome(
        string BookingItemId,
        string Name,
        string Status,
        int? PulsesUsed,
        string? Notes);
}
