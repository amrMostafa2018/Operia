using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Customers.Queries.GetCustomerPurchaseDetails;

public sealed class GetCustomerPurchaseDetailsHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IIdentityService identity,
    IDateTimeProvider clock) : IRequestHandler<GetCustomerPurchaseDetailsQuery, CustomerPurchaseDetailsDto>
{
    public async Task<CustomerPurchaseDetailsDto> Handle(
        GetCustomerPurchaseDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw UnauthorizedException.FromCode(ApiErrorCodes.Access.TenantContextRequired, "detail");
        }

        var purchase = await db.CustomerPackages.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == request.PurchaseId &&
                !x.IsTestRecord && x.DeletedAtUtc == null)
            .Select(x => new
            {
                x.Id,
                x.CustomerId,
                x.PackageId,
                x.SaleId,
                x.CreatedAt,
                x.StartedOn,
                x.ExpiresOn,
                x.IsActive,
                x.Total,
                x.Used,
                x.ReservedSessions,
                x.ReleasedSessions,
                x.PurchasePrice,
                x.AmountDue,
                x.PurchaseSnapshotJson,
                CurrentOfferType = x.Package!.OfferType,
                CurrentPulseCount = x.Package.PulseCount
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Customers.PurchaseNotFound);

        var snapshot = PackagePurchaseSnapshot.FromJson(purchase.PurchaseSnapshotJson);
        var pulseCount = snapshot?.PulseCount ?? purchase.CurrentPulseCount;
        var usesPulses = (snapshot?.OfferType ?? purchase.CurrentOfferType) == OfferType.Package && pulseCount is > 0;
        var released = snapshot?.PaymentOption == "installment"
            ? purchase.ReleasedSessions ?? (purchase.AmountDue == 0m ? purchase.Total : 0)
            : purchase.Total;
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var isBookable = purchase.IsActive && (purchase.ExpiresOn is null || purchase.ExpiresOn >= today);
        var sessions = !usesPulses
            ? new PurchaseBalanceDto(purchase.Total, purchase.Used,
                Math.Max(0, purchase.Total - purchase.Used),
                isBookable
                    ? Math.Max(0, released - purchase.Used - (purchase.ReservedSessions ?? 0))
                    : 0, released)
            : null;
        var pulses = usesPulses
            ? new PurchaseBalanceDto(purchase.Total, purchase.Used, Math.Max(0, purchase.Total - purchase.Used),
                isBookable ? Math.Max(0, released - purchase.Used) : 0, released)
            : null;

        var price = snapshot?.PurchasePrice ?? purchase.PurchasePrice;
        var historicalDue = price is not null ? purchase.AmountDue : null;
        decimal? paid = price is not null && historicalDue is not null
            ? Math.Max(0, price.Value - historicalDue.Value) : null;
        var paymentStatus = paid is null ? "unknown" : historicalDue == 0
            ? "fullyPaid" : paid > 0 ? "partial" : "unpaid";
        var packageStatus = !purchase.IsActive ? "cancelled"
            : purchase.ExpiresOn is not null && purchase.ExpiresOn < today ? "expired"
            : purchase.Total > 0 && purchase.Used >= purchase.Total ? "completed"
            : "active";

        var savedPlan = await db.CustomerPackageInstallments.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerPackageId == purchase.Id)
            .OrderBy(x => x.Number)
            .Select(x => new SavedInstallmentRow(
                x.Id,
                x.Number,
                x.Amount,
                x.CumulativeAvailableSessions,
                x.CollectedAtUtc,
                x.PaymentMethod))
            .ToListAsync(cancellationToken);
        var savedNumberedPlan = savedPlan.Where(x => x.Number > 0).ToArray();
        var hasSavedNumberedPlan = savedNumberedPlan.Length > 0;
        var schedule = hasSavedNumberedPlan
            ? savedNumberedPlan.Select(x => new InstallmentScheduleRow(
                x.Number,
                x.Amount,
                x.CumulativeAvailableSessions,
                x.CollectedAtUtc)).ToArray()
            : snapshot?.Installments.OrderBy(x => x.Number)
                .Select(x => new InstallmentScheduleRow(
                    x.Number,
                    x.Amount,
                    x.CumulativeAvailableSessions,
                    null)).ToArray() ?? [];
        var cumulativeDue = snapshot?.FirstPayment ?? 0m;
        var rows = schedule.Select(x =>
        {
            cumulativeDue += x.Amount;
            var isPaid = hasSavedNumberedPlan
                ? x.CollectedAtUtc is not null
                : paid is not null && paid >= cumulativeDue;
            return new PurchaseInstallmentDto(
                x.Number,
                x.Amount,
                isPaid ? "paid" : purchase.IsActive ? "due" : "cancelled");
        }).ToArray();
        var nextNumber = rows.FirstOrDefault(x => x.Status == "due")?.Number;
        var previous = schedule.LastOrDefault(x => x.Number < nextNumber);
        var eligible = nextNumber is not null && purchase.IsActive &&
            (purchase.ExpiresOn is null || purchase.ExpiresOn >= today) &&
            (previous is null || purchase.Used >= previous.CumulativeAvailableSessions);
        var numberedCollections = await db.InstallmentCollections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerPackageId == purchase.Id)
            .OrderBy(x => x.InstallmentNumber)
            .Select(x => new
            {
                x.Id,
                x.InstallmentNumber,
                x.Amount,
                x.PaymentMethod,
                x.ConfirmedAtUtc,
                x.IdempotencyKey,
                x.CollectedByUserId
            })
            .ToListAsync(cancellationToken);
        if (!purchase.IsActive && snapshot?.PaymentOption == "installment")
        {
            // Cancellation reduces the usable ceiling to Used. Recover the previously
            // released ceiling from confirmed plan rows without reopening entitlement.
            var collectedNumbers = numberedCollections.Select(x => x.InstallmentNumber).ToHashSet();
            var historicalRelease = purchase.AmountDue == 0m
                ? purchase.Total
                : schedule
                    .Where(x => x.CollectedAtUtc is not null || collectedNumbers.Contains(x.Number))
                    .Select(x => x.CumulativeAvailableSessions)
                    .DefaultIfEmpty(0)
                    .Max();
            historicalRelease = Math.Clamp(Math.Max(released, historicalRelease), 0, purchase.Total);
            if (sessions is not null)
            {
                sessions = sessions with { Released = historicalRelease };
            }
            if (pulses is not null)
            {
                pulses = pulses with { Released = historicalRelease };
            }
        }
        var confirmedSale = purchase.SaleId is null
            ? null
            : await db.SaleCollections.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.SaleId == purchase.SaleId && x.Confirmed)
                .OrderBy(x => x.AttemptedAtUtc)
                .Select(x => new
                {
                    x.Id,
                    x.PaymentMethod,
                    x.ConfirmedAtUtc,
                    x.AttemptedAtUtc,
                    x.IdempotencyKey,
                    x.CollectedByUserId
                })
                .FirstOrDefaultAsync(cancellationToken);
        var recorderIds = numberedCollections.Select(x => x.CollectedByUserId)
            .Append(confirmedSale?.CollectedByUserId ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();
        var recorders = await PaymentRecorderNameResolver.ResolveAsync(
            db, identity, tenantId, recorderIds, cancellationToken);
        var collections = new List<PurchaseCollectionDto>();
        var savedFirstPayment = savedPlan.FirstOrDefault(x => x.Number == 0);
        if (snapshot?.PaymentOption == "installment" &&
            (confirmedSale is not null || savedFirstPayment?.CollectedAtUtc is not null))
        {
            collections.Add(confirmedSale is not null
                ? new PurchaseCollectionDto(
                    confirmedSale.Id,
                    0,
                    savedFirstPayment?.Amount ?? snapshot.FirstPayment ?? 0m,
                    confirmedSale.PaymentMethod,
                    confirmedSale.ConfirmedAtUtc ?? confirmedSale.AttemptedAtUtc,
                    confirmedSale.IdempotencyKey,
                    PaymentRecorderNameResolver.DisplayName(confirmedSale.CollectedByUserId, recorders))
                : new PurchaseCollectionDto(
                    savedFirstPayment!.Id,
                    0,
                    savedFirstPayment.Amount,
                    savedFirstPayment.PaymentMethod ?? string.Empty,
                    savedFirstPayment.CollectedAtUtc!.Value,
                    string.Empty));
        }
        collections.AddRange(numberedCollections.Select(x => new PurchaseCollectionDto(
            x.Id,
            x.InstallmentNumber,
            x.Amount,
            x.PaymentMethod,
            x.ConfirmedAtUtc,
            x.IdempotencyKey,
            PaymentRecorderNameResolver.DisplayName(x.CollectedByUserId, recorders))));
        IReadOnlyList<string> methods = eligible
            ? await BookingPaymentMethods.GetEnabledAsync(db, tenantId, cancellationToken)
            : [];
        var completed = await LoadCompletedSessionsAsync(
            tenantId, purchase.Id, purchase.PackageId, request.AllowedBranchIds, cancellationToken);

        return new CustomerPurchaseDetailsDto(
            purchase.Id,
            purchase.CreatedAt,
            purchase.StartedOn,
            purchase.ExpiresOn,
            snapshot?.PackageExpiryMonths,
            packageStatus,
            paymentStatus,
            sessions,
            pulses,
            price,
            paid,
            historicalDue,
            nextNumber,
            rows.Length,
            rows,
            completed,
            eligible,
            schedule.FirstOrDefault(x => x.Number == nextNumber)?.Amount,
            methods,
            collections);
    }

    private async Task<IReadOnlyList<CompletedPurchaseSessionDto>> LoadCompletedSessionsAsync(
        string tenantId,
        string purchaseId,
        string packageId,
        IReadOnlyCollection<string>? allowedBranchIds,
        CancellationToken cancellationToken)
    {
        var reservations = await db.BookingPackageReservations.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerPackageId == purchaseId &&
                        x.CancellationAtUtc == null)
            .Select(x => new { x.BookingId, x.SessionNumber })
            .ToListAsync(cancellationToken);
        if (reservations.Count == 0)
        {
            return [];
        }

        var candidateIds = reservations.Select(x => x.BookingId).Distinct().ToArray();
        var bookingQuery = db.Bookings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && candidateIds.Contains(x.Id) &&
                        x.Status == BookingStatus.Completed);
        if (allowedBranchIds is not null)
        {
            bookingQuery = bookingQuery.Where(x => allowedBranchIds.Contains(x.BranchId));
        }
        var bookings = await bookingQuery
            .Select(x => new
            {
                x.Id,
                x.ScheduledDate,
                x.StartMinutes,
                x.EmployeeId
            })
            .ToListAsync(cancellationToken);
        var bookingById = bookings.ToDictionary(x => x.Id);
        if (bookingById.Count == 0)
        {
            return [];
        }

        var employeeIds = bookings.Select(x => x.EmployeeId).Distinct().ToArray();
        var performers = await db.Employees.AsNoTracking()
            .Where(x => x.TenantId == tenantId && employeeIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        var bookingIds = bookingById.Keys.ToArray();
        var histories = await db.BookingHistory.AsNoTracking()
            .Where(x => x.TenantId == tenantId && bookingIds.Contains(x.BookingId) && x.Action == "Closed")
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.BookingId, x.ChangesJson })
            .ToListAsync(cancellationToken);
        var itemIds = await db.BookingItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && bookingIds.Contains(x.BookingId) && x.PackageId == packageId)
            .Select(x => new { x.BookingId, x.Id, x.Name })
            .ToListAsync(cancellationToken);
        var itemsByBooking = itemIds.GroupBy(x => x.BookingId)
            .ToDictionary(x => x.Key, x => x.ToDictionary(item => item.Id, item => item.Name));
        var historyByBooking = histories.GroupBy(x => x.BookingId)
            .ToDictionary(x => x.Key, x => x.First().ChangesJson);
        var result = new List<CompletedPurchaseSessionDto>();

        foreach (var reservation in reservations)
        {
            if (!bookingById.TryGetValue(reservation.BookingId, out var booking) ||
                !historyByBooking.TryGetValue(reservation.BookingId, out var json) ||
                string.IsNullOrWhiteSpace(json) ||
                !itemsByBooking.TryGetValue(reservation.BookingId, out var matchingItems))
            {
                continue;
            }

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("Items", out var items))
            {
                continue;
            }

            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("Status", out var status) ||
                    !string.Equals(status.GetString(), "complete", StringComparison.OrdinalIgnoreCase) ||
                    !item.TryGetProperty("BookingItemId", out var itemId) ||
                    !matchingItems.TryGetValue(itemId.GetString() ?? string.Empty, out var sessionType))
                {
                    continue;
                }

                if (item.TryGetProperty("CustomerPackageIds", out var purchaseIds))
                {
                    if (!purchaseIds.EnumerateArray().Any(x => x.GetString() == purchaseId))
                    {
                        continue;
                    }
                }
                else if (matchingItems.Count != 1)
                {
                    continue;
                }

                var pulsesUsed = item.TryGetProperty("PulsesUsed", out var pulses) &&
                                 pulses.ValueKind == JsonValueKind.Number ? pulses.GetInt32() : (int?)null;
                var notes = item.TryGetProperty("Notes", out var note) &&
                            note.ValueKind == JsonValueKind.String ? note.GetString() : null;
                result.Add(new CompletedPurchaseSessionDto(
                    reservation.SessionNumber,
                    booking.ScheduledDate,
                    booking.StartMinutes,
                    performers.GetValueOrDefault(booking.EmployeeId),
                    pulsesUsed,
                    notes,
                    sessionType));
                break;
            }
        }

        return result.OrderBy(x => x.Date).ThenBy(x => x.StartMinutes).ThenBy(x => x.SessionNumber).ToArray();
    }

    private sealed record SavedInstallmentRow(
        string Id,
        int Number,
        decimal Amount,
        int CumulativeAvailableSessions,
        DateTime? CollectedAtUtc,
        string? PaymentMethod);

    private sealed record InstallmentScheduleRow(
        int Number,
        decimal Amount,
        int CumulativeAvailableSessions,
        DateTime? CollectedAtUtc);
}
