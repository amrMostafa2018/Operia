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
                            join sale in db.Sales.AsNoTracking() on purchase.SaleId equals sale.Id
                            join customer in db.Customers.AsNoTracking() on purchase.CustomerId equals customer.Id
                            where purchase.TenantId == tenantId && sale.TenantId == tenantId &&
                                  customer.TenantId == tenantId &&
                                  purchase.Id == request.PurchaseId &&
                                  purchase.CustomerId == request.CustomerId &&
                                  sale.CustomerId == request.CustomerId &&
                                  allowed.Contains(sale.BranchId)
                            select new { Purchase = purchase, Customer = customer, SaleId = sale.Id })
            .SingleOrDefaultAsync(token)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Customers.PurchaseNotFound);
        var confirmed = await db.SaleCollections.AsNoTracking().AnyAsync(x =>
            x.TenantId == tenantId && x.SaleId == source.SaleId && x.Confirmed, token);
        var snapshot = PackagePurchaseSnapshot.FromJson(source.Purchase.PurchaseSnapshotJson);
        var initial = !confirmed ? 0m : snapshot?.PaymentOption == "installment"
            ? snapshot.FirstPayment ?? 0m : source.Purchase.PurchasePrice ?? 0m;
        var installments = await db.InstallmentCollections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerPackageId == request.PurchaseId)
            .SumAsync(x => (decimal?)x.Amount, token) ?? 0m;
        var refunded = await db.RefundTransactions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerPackageId == request.PurchaseId)
            .SumAsync(x => (decimal?)x.Amount, token) ?? 0m;
        var paid = initial + installments;
        return new(source.Customer.Id, source.Customer.FullName,
            source.Customer.MobileNumber, Convert.ToBase64String(source.Customer.Version),
            source.Customer.IsBlocked, source.Customer.BlockReason,
            source.Purchase.Id, source.Purchase.IsActive ? "active" : "cancelled",
            paid, refunded, Math.Max(0m, paid - refunded));
    }
}
