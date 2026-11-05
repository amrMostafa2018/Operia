using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Sales.Commands.ConfirmSale;
using Operia.Application.Packages;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Revenue.Queries.ListActivityRevenue;

public sealed class ListActivityRevenueHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope) : IRequestHandler<ListActivityRevenueQuery, ActivityRevenuePage>
{
    public async Task<ActivityRevenuePage> Handle(
        ListActivityRevenueQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var ledger = await new RevenueLedger(db, currentUser, branchScope)
            .ScopedAsync(request.BranchId, cancellationToken);
        var installments = ledger.Installments
            .Select(x => new
            {
                x.Id,
                CustomerName = x.CustomerPackage!.Customer!.FullName,
                OfferName = x.CustomerPackage.Package!.Name,
                SnapshotJson = x.CustomerPackage.PurchaseSnapshotJson,
                x.CustomerPackageId,
                x.InstallmentNumber,
                x.Amount,
                x.PaymentMethod,
                x.ConfirmedAtUtc
            });
        var saleCollections =
            from collection in ledger.Sales
            join sale in db.Sales.AsNoTracking() on collection.SaleId equals sale.Id
            join customer in db.Customers.AsNoTracking() on sale.CustomerId equals customer.Id
            where sale.TenantId == tenantId && customer.TenantId == tenantId
            select new
            {
                collection.Id,
                SaleId = sale.Id,
                CustomerName = customer.FullName,
                sale.ItemsSnapshotJson,
                collection.Amount,
                collection.PaymentMethod,
                ConfirmedAtUtc = collection.ConfirmedAtUtc ?? collection.AttemptedAtUtc
            };
        var total = await installments.CountAsync(cancellationToken) +
            await saleCollections.CountAsync(cancellationToken);
        var refunds = from refund in ledger.Refunds
                      join customer in db.Customers.AsNoTracking() on refund.CustomerId equals customer.Id
                      join purchase in db.CustomerPackages.AsNoTracking()
                          on refund.CustomerPackageId equals purchase.Id
                      where customer.TenantId == tenantId && purchase.TenantId == tenantId
                      select new
                      {
                          refund.Id,
                          refund.SaleId,
                          refund.CustomerPackageId,
                          CustomerName = customer.FullName,
                          purchase.PurchaseSnapshotJson,
                          refund.Amount,
                          refund.RecordedAtUtc
                      };
        total += await refunds.CountAsync(cancellationToken);
        var confirmedTotal = await installments.SumAsync(x => (decimal?)x.Amount,
            cancellationToken) ?? 0m;
        confirmedTotal += await saleCollections.SumAsync(x => (decimal?)x.Amount,
            cancellationToken) ?? 0m;
        confirmedTotal -= await refunds.SumAsync(x => (decimal?)x.Amount,
            cancellationToken) ?? 0m;
        var page = Math.Clamp(request.Page, 1, 100_000);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var candidateCount = page * pageSize;
        var installmentRows = await installments.OrderByDescending(x => x.ConfirmedAtUtc)
            .ThenByDescending(x => x.Id).Take(candidateCount)
            .ToListAsync(cancellationToken);
        var saleRows = await saleCollections.OrderByDescending(x => x.ConfirmedAtUtc)
            .ThenByDescending(x => x.Id).Take(candidateCount)
            .ToListAsync(cancellationToken);
        var refundRows = await refunds.OrderByDescending(x => x.RecordedAtUtc)
            .ThenByDescending(x => x.Id).Take(candidateCount).ToListAsync(cancellationToken);
        return new ActivityRevenuePage(total, confirmedTotal,
            installmentRows.Select(x => new ActivityRevenueRow(
                x.Id, null, x.CustomerPackageId, x.CustomerName,
                PackagePurchaseSnapshot.FromJson(x.SnapshotJson)?.OfferName ?? x.OfferName,
                x.InstallmentNumber, x.Amount, x.PaymentMethod,
                x.ConfirmedAtUtc, "Installment"))
                .Concat(saleRows.Select(x => new ActivityRevenueRow(
                    x.Id, x.SaleId, null, x.CustomerName,
                    SaleNames(x.ItemsSnapshotJson), null, x.Amount,
                    x.PaymentMethod, x.ConfirmedAtUtc, "Sale")))
                .Concat(refundRows.Select(x => new ActivityRevenueRow(
                    x.Id, x.SaleId, x.CustomerPackageId, x.CustomerName,
                    PackagePurchaseSnapshot.FromJson(x.PurchaseSnapshotJson)?.OfferName ?? string.Empty,
                    null, -x.Amount, string.Empty, x.RecordedAtUtc, "Refund")))
                .OrderByDescending(x => x.ConfirmedAtUtc).ThenByDescending(x => x.CollectionId)
                .Skip((page - 1) * pageSize).Take(pageSize).ToArray());
    }

    private static string SaleNames(string? json)
    {
        var lines = JsonSerializer.Deserialize<ConfirmedSaleItemSnapshot[]>(json ?? "[]") ?? [];
        return string.Join(", ", lines.Select(x => x.Name));
    }
}
