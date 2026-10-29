using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.Auditing;
using Operia.Domain.Entities;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Revenue.Queries.GetRevenuePaymentHistory;

public sealed class GetRevenuePaymentHistoryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IBranchScope branchScope)
    : IRequestHandler<GetRevenuePaymentHistoryQuery, IReadOnlyList<RevenuePaymentHistoryRow>>
{
    public async Task<IReadOnlyList<RevenuePaymentHistoryRow>> Handle(
        GetRevenuePaymentHistoryQuery request, CancellationToken token)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var allowed = await branchScope.GetAllowedBranchIdsAsync(
            currentUser.UserId ?? string.Empty, token);
        var salePayment = await (
            from payment in db.SaleCollections.AsNoTracking()
            join sale in db.Sales.AsNoTracking() on payment.SaleId equals sale.Id
            where payment.TenantId == tenantId && sale.TenantId == tenantId &&
                  payment.Id == request.PaymentId
            select new { SaleId = sale.Id, sale.BranchId }).SingleOrDefaultAsync(token);
        var installment = await (
            from payment in db.InstallmentCollections.AsNoTracking()
            join purchase in db.CustomerPackages.AsNoTracking()
                on payment.CustomerPackageId equals purchase.Id
            join sale in db.Sales.AsNoTracking() on purchase.SaleId equals sale.Id
            where payment.TenantId == tenantId && purchase.TenantId == tenantId &&
                  sale.TenantId == tenantId && payment.Id == request.PaymentId
            select new { PurchaseId = purchase.Id, sale.BranchId })
            .SingleOrDefaultAsync(token);
        var refundPayment = await (
            from refund in db.RefundTransactions.AsNoTracking()
            join sale in db.Sales.AsNoTracking() on refund.SaleId equals sale.Id
            where refund.TenantId == tenantId && sale.TenantId == tenantId &&
                  refund.Id == request.PaymentId
            select new { RefundId = refund.Id, sale.BranchId }).SingleOrDefaultAsync(token);
        if (salePayment is null && installment is null && refundPayment is null)
        {
            throw ApiNotFoundException.FromCode(ApiErrorCodes.Revenue.PaymentNotFound);
        }
        var branchId = salePayment?.BranchId ?? installment?.BranchId ??
            refundPayment?.BranchId;
        if (!allowed.Contains(branchId!))
        {
            throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantAccessDenied);
        }
        var saleId = salePayment?.SaleId;
        var purchaseId = installment?.PurchaseId;
        var refundId = refundPayment?.RefundId;
        var events = await db.AuditLogs.AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                (x.EntityType == nameof(SaleCollection) &&
                    x.EntityId == request.PaymentId ||
                 saleId != null && x.EntityType == nameof(Sale) &&
                    x.EntityId == saleId && x.DetailsJson != null &&
                    x.DetailsJson.Contains(request.PaymentId) &&
                    (x.Action == AuditActions.SalePaymentConfirmed ||
                     x.Action == AuditActions.SaleCollectionAttempted) ||
                 purchaseId != null && x.EntityType == nameof(CustomerPackage) &&
                    x.EntityId == purchaseId && x.Action == AuditActions.InstallmentCollected &&
                    x.DetailsJson != null && x.DetailsJson.Contains(request.PaymentId) ||
                 refundId != null && x.EntityType == nameof(RefundTransaction) &&
                    x.EntityId == refundId && x.Action == AuditActions.RefundPosted))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.CreatedAt, x.UserDisplayName, x.UserId,
                x.Action, x.DetailsJson })
            .ToListAsync(token);
        return events.OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Action == AuditActions.RevenuePaymentSubmitted ? 0 : 1)
            .ThenBy(x => x.Id).Select(x =>
        {
            using var details = JsonDocument.Parse(x.DetailsJson ?? "{}");
            return new RevenuePaymentHistoryRow(x.CreatedAt,
                x.UserDisplayName ?? x.UserId ?? string.Empty, x.Action,
                x.Action is AuditActions.SalePaymentConfirmed or
                    AuditActions.SaleCollectionAttempted ? "Pending" :
                    Field(details.RootElement, "oldStatus"),
                x.Action switch
                {
                    AuditActions.SalePaymentConfirmed => "Paid",
                    AuditActions.SaleCollectionAttempted => "Failed",
                    AuditActions.InstallmentCollected => "Paid",
                    AuditActions.RefundPosted => "Refunded",
                    _ => Field(details.RootElement, "newStatus")
                });
        }).ToArray();
    }

    private static string? Field(JsonElement details, string key) =>
        details.TryGetProperty(key, out var value) ? value.GetString() : null;
}
