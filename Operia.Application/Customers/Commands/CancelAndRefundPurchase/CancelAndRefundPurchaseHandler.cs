using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Customers.Commands.CancelAndRefundPurchase;

public sealed class CancelAndRefundPurchaseHandler(
    IApplicationDbContext db, ICustomerRefundStore store, ICurrentUserService currentUser,
    IBranchScope branchScope, IAuditWriter auditWriter, IDateTimeProvider clock)
    : IRequestHandler<CancelAndRefundPurchaseCommand, CancelAndRefundPurchaseResult>
{
    public async Task<CancelAndRefundPurchaseResult> Handle(
        CancelAndRefundPurchaseCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var allowed = await branchScope.GetAllowedBranchIdsAsync(
            currentUser.UserId ?? string.Empty, cancellationToken);
        var saleBranch = await (from purchase in db.CustomerPackages.AsNoTracking()
                                join sale in db.Sales.AsNoTracking() on purchase.SaleId equals sale.Id
                                where purchase.TenantId == tenantId && sale.TenantId == tenantId &&
                                      purchase.Id == request.PurchaseId &&
                                      allowed.Contains(sale.BranchId)
                                select sale.BranchId).SingleOrDefaultAsync(cancellationToken);
        if (saleBranch is null)
        {
            throw ApiNotFoundException.FromCode(ApiErrorCodes.Customers.PurchaseNotFound);
        }

        return await store.ExecuteAsync(tenantId, request.PurchaseId,
            token => PostUnderLockAsync(request, tenantId, allowed, token), cancellationToken);
    }

    private async Task<CancelAndRefundPurchaseResult> PostUnderLockAsync(
        CancelAndRefundPurchaseCommand request, string tenantId,
        IReadOnlyCollection<string> allowedBranches, CancellationToken token)
    {
        var purchase = await db.CustomerPackages.SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.Id == request.PurchaseId, token)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Customers.PurchaseNotFound);
        var sale = await db.Sales.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.Id == purchase.SaleId &&
            x.CustomerId == purchase.CustomerId && allowedBranches.Contains(x.BranchId), token)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Customers.PurchaseNotFound);
        var previous = await db.RefundTransactions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerPackageId == purchase.Id)
            .ToListAsync(token);
        var paid = await PaidAsync(tenantId, purchase, sale.Id, token);
        var refunded = previous.Sum(x => x.Amount);
        var existing = previous.SingleOrDefault(x => x.IdempotencyKey == request.IdempotencyKey);
        if (existing is not null)
        {
            if (existing.Amount != request.Amount || existing.Note != request.Note)
            {
                throw ConflictException.FromCode(ApiErrorCodes.Customers.RefundIdempotencyConflict);
            }
            return new(existing.Id, existing.Amount, refunded,
                Math.Max(0m, paid - refunded), "cancelled");
        }
        if (purchase.PurchasePrice is null || paid <= 0m)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Customers.RefundUnavailable);
        }
        if (request.Amount > paid - refunded)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Customers.RefundExceedsPaid, "amount");
        }

        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var futureBookingIds = await (from reservation in db.BookingPackageReservations.AsNoTracking()
                                      join booking in db.Bookings.AsNoTracking()
                                          on reservation.BookingId equals booking.Id
                                      where reservation.TenantId == tenantId &&
                                            booking.TenantId == tenantId &&
                                            reservation.CustomerPackageId == purchase.Id &&
                                            reservation.CancellationAtUtc == null &&
                                            booking.Status == BookingStatus.Booked &&
                                            booking.ScheduledDate >= today
                                      select booking.Id).Distinct().ToArrayAsync(token);
        var bookings = await db.Bookings.Where(x => x.TenantId == tenantId &&
            futureBookingIds.Contains(x.Id)).ToListAsync(token);
        if (bookings.Any(x => !allowedBranches.Contains(x.BranchId)))
        {
            throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantAccessDenied);
        }
        var reservations = await db.BookingPackageReservations.Where(x =>
            x.TenantId == tenantId && futureBookingIds.Contains(x.BookingId) &&
            x.CancellationAtUtc == null).ToListAsync(token);
        var balanceIds = reservations.Select(x => x.CustomerPackageId).Distinct().ToArray();
        var balances = await db.CustomerPackages.Where(x => x.TenantId == tenantId &&
            balanceIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, token);
        foreach (var reservation in reservations)
        {
            if (!balances.TryGetValue(reservation.CustomerPackageId, out var balance))
            {
                throw ConflictException.FromCode(ApiErrorCodes.Customers.RefundUnavailable);
            }
            reservation.CancellationAtUtc = now;
            balance.ReservedSessions = Math.Max(0, balance.ReservedSessionCount - 1);
        }
        foreach (var booking in bookings)
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
                    Reason = "PurchaseCancelled",
                    purchase.Id,
                    OldStatus = BookingStatus.Booked,
                    NewStatus = BookingStatus.Cancelled
                })
            });
            auditWriter.Write(tenantId, AuditActions.BookingCancelled, nameof(Booking),
                booking.Id, booking.BookingNumber, new
                {
                    Reason = "PurchaseCancelled",
                    purchase.Id,
                    OldStatus = BookingStatus.Booked,
                    NewStatus = BookingStatus.Cancelled
                });
        }
        var wasActive = purchase.IsActive;
        purchase.IsActive = false;
        purchase.ReleasedSessions = purchase.Used;
        var refund = new RefundTransaction
        {
            TenantId = tenantId,
            CustomerPackageId = purchase.Id,
            SaleId = sale.Id,
            CustomerId = purchase.CustomerId,
            IdempotencyKey = request.IdempotencyKey,
            Amount = request.Amount,
            Note = request.Note,
            RecordedByUserId = currentUser.UserId ?? string.Empty,
            RecordedAtUtc = now
        };
        db.RefundTransactions.Add(refund);
        if (wasActive)
        {
            auditWriter.Write(tenantId, AuditActions.PurchaseCancelled,
                nameof(CustomerPackage), purchase.Id,
                PackagePurchaseSnapshot.FromJson(purchase.PurchaseSnapshotJson)?.OfferName ?? purchase.Id,
                new { OldStatus = "active", NewStatus = "cancelled", CancelledBookings = bookings.Count });
        }
        auditWriter.Write(tenantId, AuditActions.RefundPosted, nameof(RefundTransaction),
            refund.Id, purchase.Id,
            new
            {
                refund.CustomerPackageId,
                refund.SaleId,
                refund.Amount,
                refund.Note,
                PreviousRefunds = refunded,
                Paid = paid
            });
        await db.SaveChangesAsync(token);
        return new(refund.Id, refund.Amount, refunded + refund.Amount,
            Math.Max(0m, paid - refunded - refund.Amount), "cancelled");
    }

    private async Task<decimal> PaidAsync(string tenantId, CustomerPackage purchase,
        string saleId, CancellationToken token)
    {
        var confirmed = await db.SaleCollections.AsNoTracking().AnyAsync(x =>
            x.TenantId == tenantId && x.SaleId == saleId && x.Confirmed, token);
        if (!confirmed) return 0m;
        var snapshot = PackagePurchaseSnapshot.FromJson(purchase.PurchaseSnapshotJson);
        var initial = snapshot?.PaymentOption == "installment"
            ? snapshot.FirstPayment ?? 0m : purchase.PurchasePrice ?? 0m;
        var installments = await db.InstallmentCollections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerPackageId == purchase.Id)
            .SumAsync(x => (decimal?)x.Amount, token) ?? 0m;
        return initial + installments;
    }
}
