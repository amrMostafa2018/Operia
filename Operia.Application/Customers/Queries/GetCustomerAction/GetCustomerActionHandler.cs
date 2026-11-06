using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Customers.Queries.GetCustomerAction;

public sealed class GetCustomerActionHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IBranchScope branchScope)
    : IRequestHandler<GetCustomerActionQuery, CustomerActionDto>
{
    public async Task<CustomerActionDto> Handle(GetCustomerActionQuery request, CancellationToken token)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var allowed = await branchScope.GetAllowedBranchIdsAsync(
            currentUser.UserId ?? string.Empty, token);
        var source = await (from purchase in db.CustomerPackages.AsNoTracking()
                            join linkedSale in db.Sales.AsNoTracking().Where(x => x.TenantId == tenantId)
                                on purchase.SaleId equals linkedSale.Id into saleGroup
                            from sale in saleGroup.DefaultIfEmpty()
                            join customer in db.Customers.AsNoTracking() on purchase.CustomerId equals customer.Id
                            where purchase.TenantId == tenantId &&
                                  customer.TenantId == tenantId &&
                                  purchase.Id == request.PurchaseId &&
                                  purchase.CustomerId == request.CustomerId &&
                                  (sale == null || sale.CustomerId == request.CustomerId) &&
                                  allowed.Contains(purchase.BranchId ?? sale!.BranchId)
                            select new
                            {
                                Purchase = purchase,
                                Customer = customer,
                                SaleId = sale == null ? null : sale.Id
                            })
            .SingleOrDefaultAsync(token)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Customers.PurchaseNotFound);
        var confirmed = await db.SaleCollections.AsNoTracking().AnyAsync(x =>
            x.TenantId == tenantId && x.SaleId == source.SaleId && x.Confirmed, token);
        var snapshot = PackagePurchaseSnapshot.FromJson(source.Purchase.PurchaseSnapshotJson);
        var initial = source.SaleId is null
            ? source.Purchase.PurchasePrice is { } price && source.Purchase.AmountDue is { } due
                ? Math.Max(0m, price - due) : 0m
            : !confirmed ? 0m : snapshot?.PaymentOption == "installment"
            ? snapshot.FirstPayment ?? 0m : source.Purchase.PurchasePrice ?? 0m;
        var installments = await db.InstallmentCollections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerPackageId == request.PurchaseId)
            .SumAsync(x => (decimal?)x.Amount, token) ?? 0m;
        var refunded = await db.RefundTransactions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerPackageId == request.PurchaseId)
            .SumAsync(x => (decimal?)x.Amount, token) ?? 0m;
        var paid = source.SaleId is null ? initial : initial + installments;
        return new(source.Customer.Id, source.Customer.FullName,
            source.Customer.MobileNumber, Convert.ToBase64String(source.Customer.Version),
            source.Customer.IsBlocked, source.Customer.BlockReason,
            source.Purchase.Id, source.Purchase.IsActive ? "active" : "cancelled",
            paid, refunded, Math.Max(0m, paid - refunded));
    }
}
