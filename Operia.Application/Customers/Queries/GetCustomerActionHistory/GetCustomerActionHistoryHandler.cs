using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Customers.Queries.GetCustomerActionHistory;

public sealed class GetCustomerActionHistoryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IBranchScope branchScope)
    : IRequestHandler<GetCustomerActionHistoryQuery, CustomerActionHistoryPage>
{
    public async Task<CustomerActionHistoryPage> Handle(
        GetCustomerActionHistoryQuery request, CancellationToken token)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var allowed = await branchScope.GetAllowedBranchIdsAsync(
            currentUser.UserId ?? string.Empty, token);
        var purchaseExists = await (from purchase in db.CustomerPackages.AsNoTracking()
                                    join sale in db.Sales.AsNoTracking() on purchase.SaleId equals sale.Id
                                    where purchase.TenantId == tenantId && sale.TenantId == tenantId &&
                                          purchase.Id == request.PurchaseId &&
                                          purchase.CustomerId == request.CustomerId &&
                                          sale.CustomerId == request.CustomerId &&
                                          allowed.Contains(sale.BranchId)
                                    select purchase.Id).AnyAsync(token);
        if (!purchaseExists)
        {
            throw ApiNotFoundException.FromCode(ApiErrorCodes.Customers.PurchaseNotFound);
        }
        var refundIds = await db.RefundTransactions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerPackageId == request.PurchaseId)
            .Select(x => x.Id).ToArrayAsync(token);
        var events = db.AuditLogs.AsNoTracking().Where(x => x.TenantId == tenantId &&
            (x.EntityType == "Customer" && x.EntityId == request.CustomerId &&
                (x.Action == AuditActions.CustomerBlocked || x.Action == AuditActions.CustomerUnblocked) ||
             x.EntityType == "CustomerPackage" && x.EntityId == request.PurchaseId &&
                x.Action == AuditActions.PurchaseCancelled ||
             x.EntityType == "RefundTransaction" && refundIds.Contains(x.EntityId) &&
                x.Action == AuditActions.RefundPosted));
        var total = await events.CountAsync(token);
        var page = Math.Clamp(request.Page, 1, 100_000);
        var rows = await events.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * 10).Take(10)
            .Select(x => new
            {
                x.Id,
                x.CreatedAt,
                x.Action,
                x.UserDisplayName,
                x.UserId,
                x.DetailsJson
            }).ToListAsync(token);
        return new(total, page, rows.Select(x =>
        {
            decimal? amount = null;
            string? reason = null;
            if (!string.IsNullOrWhiteSpace(x.DetailsJson))
            {
                using var details = JsonDocument.Parse(x.DetailsJson);
                if (details.RootElement.TryGetProperty("amount", out var value) &&
                    value.ValueKind == JsonValueKind.Number)
                {
                    amount = value.GetDecimal();
                }
                if (details.RootElement.TryGetProperty("note", out value) &&
                    value.ValueKind == JsonValueKind.String)
                {
                    reason = value.GetString();
                }
                if (details.RootElement.TryGetProperty("newReason", out value) &&
                    value.ValueKind == JsonValueKind.String)
                {
                    reason = value.GetString();
                }
            }
            return new CustomerActionHistoryRow(x.Id, x.CreatedAt, x.Action,
                x.UserDisplayName ?? x.UserId, amount, reason);
        }).ToArray());
    }
}
