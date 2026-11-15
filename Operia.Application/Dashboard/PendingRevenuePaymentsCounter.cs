using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Revenue;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Dashboard;

/// <summary>Counts sale collections awaiting electronic payment review within branch scope.</summary>
internal static class PendingRevenuePaymentsCounter
{
    public static async Task<int> CountAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBranchScope branchScope,
        string? branchId,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var ledger = await new RevenueLedger(db, currentUser, branchScope)
            .ScopedAsync(branchId, cancellationToken);

        return await (
                from collection in ledger.SaleAttempts
                join sale in db.Sales.AsNoTracking() on collection.SaleId equals sale.Id
                where sale.TenantId == tenantId
                      && collection.ReviewStatus == "UnderReview"
                      && sale.Status == "PendingCollection"
                select collection)
            .CountAsync(cancellationToken);
    }
}