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
            .Where(x => x.TenantId == tenantId && x.Id == request.PurchaseId)
            .Select(x => new
            {
                x.Id,
                x.CustomerId,
                x.PackageId,
                x.CreatedAt,
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
        var released = snapshot?.PaymentOption == "installment" && purchase.AmountDue > 0m
            ? purchase.ReleasedSessions ?? 0
            : purchase.Total;
        var sessions = !usesPulses
            ? new PurchaseBalanceDto(purchase.Total, purchase.Used,
                Math.Max(0, purchase.Total - purchase.Used),
                Math.Max(0, released - purchase.Used - (purchase.ReservedSessions ?? 0)))
            : null;
        var pulses = usesPulses
            ? new PurchaseBalanceDto(purchase.Total, purchase.Used, Math.Max(0, purchase.Total - purchase.Used))
            : null;

        var price = snapshot?.PurchasePrice ?? purchase.PurchasePrice;
        var due = price is not null ? purchase.AmountDue : null;
        decimal? paid = price is not null && due is not null ? Math.Max(0, price.Value - due.Value) : null;
        var paymentStatus = paid is null ? "unknown" : due == 0 ? "fullyPaid" : paid > 0 ? "partial" : "unpaid";
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var packageStatus = !purchase.IsActive ? "cancelled"
            : purchase.ExpiresOn is not null && purchase.ExpiresOn < today ? "expired"
            : purchase.Total > 0 && purchase.Used >= purchase.Total ? "completed"
            : "active";

        var cumulativeDue = snapshot?.FirstPayment ?? 0m;
        var rows = snapshot?.Installments.OrderBy(x => x.Number)
            .Select(x =>
            {
                cumulativeDue += x.Amount;
                return new PurchaseInstallmentDto(
                    x.Number,
                    x.Amount,
                    paid is not null && paid >= cumulativeDue ? "paid" : "due");
            })
            .ToArray() ?? [];
        var nextNumber = rows.FirstOrDefault(x => x.Status == "due")?.Number;
        var previous = snapshot?.Installments.LastOrDefault(x => x.Number < nextNumber);
        var eligible = nextNumber is not null && purchase.IsActive &&
            (purchase.ExpiresOn is null || purchase.ExpiresOn >= today) &&
            (previous is null || purchase.Used >= previous.CumulativeAvailableSessions);
        var collections = await db.InstallmentCollections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerPackageId == purchase.Id)
            .OrderBy(x => x.InstallmentNumber)
            .Select(x => new PurchaseCollectionDto(x.Id, x.InstallmentNumber, x.Amount,
                x.PaymentMethod, x.ConfirmedAtUtc, x.IdempotencyKey))
            .ToListAsync(cancellationToken);
        IReadOnlyList<string> methods = eligible
            ? await BookingPaymentMethods.GetEnabledAsync(db, tenantId, cancellationToken)
            : [];
        var completed = await LoadCompletedSessionsAsync(
            tenantId, purchase.Id, purchase.PackageId, request.AllowedBranchIds, cancellationToken);

        return new CustomerPurchaseDetailsDto(
            purchase.Id,
            purchase.CreatedAt,
            null,
            purchase.ExpiresOn,
            snapshot?.PackageExpiryMonths,
            packageStatus,
            paymentStatus,
            sessions,
            pulses,
            price,
            paid,
            due,
            nextNumber,
            rows.Length,
            rows,
            completed,
            eligible,
            rows.FirstOrDefault(x => x.Number == nextNumber)?.Amount,
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
            .Select(x => new { x.BookingId, x.Id })
            .ToListAsync(cancellationToken);
        var itemsByBooking = itemIds.GroupBy(x => x.BookingId)
            .ToDictionary(x => x.Key, x => x.Select(item => item.Id).ToHashSet());
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
                    !matchingItems.Contains(itemId.GetString() ?? string.Empty))
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
                    notes));
                break;
            }
        }

        return result.OrderBy(x => x.Date).ThenBy(x => x.StartMinutes).ThenBy(x => x.SessionNumber).ToArray();
    }
}
