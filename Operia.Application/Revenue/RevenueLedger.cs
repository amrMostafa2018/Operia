using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Revenue;

/// <summary>Confirmed financial movements scoped to the caller's sale branches.</summary>
internal sealed class RevenueLedger(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope)
{
    public async Task<RevenueMovements> ScopedAsync(string? branchId, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var userId = currentUser.UserId
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");
        var allowed = await branchScope.GetAllowedBranchIdsAsync(userId, cancellationToken);
        if (branchId is not null && !allowed.Contains(branchId, StringComparer.Ordinal))
        {
            throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantAccessDenied, "branchId");
        }

        var branches = branchId is null ? allowed.ToArray() : [branchId];
        var sales = db.Sales.AsNoTracking()
            .Where(x => x.TenantId == tenantId && branches.Contains(x.BranchId));

        var collections = from collection in db.SaleCollections.AsNoTracking()
                          join sale in sales on collection.SaleId equals sale.Id
                          where collection.TenantId == tenantId && collection.Confirmed &&
                                collection.Amount > 0m
                          select collection;
        var installments = from collection in db.InstallmentCollections.AsNoTracking()
                           join purchase in db.CustomerPackages.AsNoTracking()
                               on collection.CustomerPackageId equals purchase.Id
                           join sale in sales on purchase.SaleId equals sale.Id
                           where collection.TenantId == tenantId && purchase.TenantId == tenantId
                           select collection;
        var refunds = from refund in db.RefundTransactions.AsNoTracking()
                      join purchase in db.CustomerPackages.AsNoTracking()
                          on refund.CustomerPackageId equals purchase.Id
                      join sale in sales on refund.SaleId equals sale.Id
                      where refund.TenantId == tenantId && purchase.TenantId == tenantId &&
                            refund.SaleId == purchase.SaleId
                      select refund;

        return new RevenueMovements(collections, installments, refunds);
    }
}

internal sealed record RevenueMovements(
    IQueryable<SaleCollection> Sales,
    IQueryable<InstallmentCollection> Installments,
    IQueryable<RefundTransaction> Refunds)
{
    public async Task<RevenuePeriod> ForPeriodAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var saleRows = Sales.Where(x => x.AttemptedAtUtc >= fromUtc && x.AttemptedAtUtc < toUtc);
        var installmentRows = Installments.Where(x =>
            x.ConfirmedAtUtc >= fromUtc && x.ConfirmedAtUtc < toUtc);
        var refundRows = Refunds.Where(x => x.RecordedAtUtc >= fromUtc && x.RecordedAtUtc < toUtc);
        var saleAmount = await saleRows.SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var installmentAmount = await installmentRows.SumAsync(x => (decimal?)x.Amount,
            cancellationToken) ?? 0m;
        var refundAmount = await refundRows.SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var count = await saleRows.CountAsync(cancellationToken) +
            await installmentRows.CountAsync(cancellationToken);
        return new RevenuePeriod(count, saleAmount + installmentAmount, refundAmount);
    }
}

internal sealed record RevenuePeriod(int Count, decimal Gross, decimal Refunds)
{
    public decimal Net => Gross - Refunds;
}
