using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Customers.Queries.GetCustomerPurchaseDetails;
using Operia.Application.Packages;
using Operia.Application.Sales.Commands.ConfirmSale;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Customers.Queries.GetCustomerStatement;

public sealed class GetCustomerStatementHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IMediator mediator)
    : IRequestHandler<GetCustomerStatementQuery, CustomerStatementDto>
{
    public async Task<CustomerStatementDto> Handle(
        GetCustomerStatementQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var allowedBranches = await branchScope.GetAllowedBranchIdsAsync(
            currentUser.UserId ?? string.Empty, cancellationToken);

        // A purchase without a linked sale has no reliable branch provenance.
        var source = await (from purchase in db.CustomerPackages.AsNoTracking()
                            join sale in db.Sales.AsNoTracking() on purchase.SaleId equals sale.Id
                            join customer in db.Customers.AsNoTracking() on purchase.CustomerId equals customer.Id
                            where purchase.TenantId == tenantId && sale.TenantId == tenantId &&
                                  customer.TenantId == tenantId &&
                                  purchase.Id == request.PurchaseId &&
                                  purchase.CustomerId == request.CustomerId &&
                                  sale.CustomerId == request.CustomerId &&
                                  allowedBranches.Contains(sale.BranchId)
                            select new
                            {
                                purchase.SaleItemIndex,
                                purchase.PurchaseSnapshotJson,
                                customer.FullName,
                                customer.MobileNumber,
                                customer.IsActive,
                                customer.IsBlocked,
                                sale.ItemsSnapshotJson,
                                SaleId = sale.Id
                            }).SingleOrDefaultAsync(cancellationToken)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Customers.PurchaseNotFound);

        var purchaseDetails = await mediator.Send(
            new GetCustomerPurchaseDetailsQuery(request.PurchaseId, allowedBranches), cancellationToken);
        var snapshot = PackagePurchaseSnapshot.FromJson(source.PurchaseSnapshotJson);
        var saleItems = JsonSerializer.Deserialize<ConfirmedSaleItemSnapshot[]>(source.ItemsSnapshotJson) ?? [];
        var item = source.SaleItemIndex is { } index && index >= 0 && index < saleItems.Length
            ? saleItems[index] : null;
        var operationCount = await (from purchase in db.CustomerPackages.AsNoTracking()
                                    join sale in db.Sales.AsNoTracking() on purchase.SaleId equals sale.Id
                                    where purchase.TenantId == tenantId && sale.TenantId == tenantId &&
                                          purchase.CustomerId == request.CustomerId &&
                                          sale.CustomerId == request.CustomerId &&
                                          allowedBranches.Contains(sale.BranchId)
                                    select purchase.Id).CountAsync(cancellationToken);

        var collections = await db.InstallmentCollections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerPackageId == request.PurchaseId)
            .OrderBy(x => x.InstallmentNumber)
            .ToListAsync(cancellationToken);
        var confirmedSale = await db.SaleCollections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.SaleId == source.SaleId && x.Confirmed)
            .OrderBy(x => x.AttemptedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var recorderIds = collections.Select(x => x.CollectedByUserId)
            .Append(confirmedSale?.CollectedByUserId ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        var recorders = await db.Employees.AsNoTracking()
            .Where(x => x.TenantId == tenantId && recorderIds.Contains(x.IdentityUserId))
            .ToDictionaryAsync(x => x.IdentityUserId, x => x.FullName, cancellationToken);

        var installments = (snapshot?.Installments ?? [])
            .OrderBy(x => x.Number)
            .Select(plan =>
            {
                var collection = collections.FirstOrDefault(x => x.InstallmentNumber == plan.Number);
                return new StatementPaymentDto(plan.Number, plan.Amount,
                    collection is null
                        ? purchaseDetails.PackageStatus == "cancelled" ? "cancelled" : "due"
                        : "paid", collection?.ConfirmedAtUtc,
                    collection?.PaymentMethod,
                    collection is null ? null : RecorderName(collection.CollectedByUserId, recorders),
                    null);
            }).ToArray();
        var firstPayment = confirmedSale is not null && snapshot?.FirstPayment is > 0m
            ? new StatementFirstPaymentDto(snapshot.FirstPayment.Value,
                confirmedSale.ConfirmedAtUtc ?? confirmedSale.AttemptedAtUtc,
                confirmedSale.PaymentMethod,
                RecorderName(confirmedSale.CollectedByUserId, recorders))
            : null;
        var paid = snapshot?.PaymentOption == "installment"
            ? (firstPayment?.Amount ?? 0m) + collections.Sum(x => x.Amount)
            : confirmedSale is null ? 0m : purchaseDetails.PurchasePrice ?? 0m;
        var refunded = await db.RefundTransactions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerPackageId == request.PurchaseId)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        purchaseDetails = purchaseDetails with
        {
            AmountPaid = paid,
            AmountRemaining = purchaseDetails.PurchasePrice is { } price
                ? purchaseDetails.PackageStatus == "cancelled"
                    ? 0m : Math.Max(0m, price - paid) : null,
            PaymentStatus = purchaseDetails.PurchasePrice is not { } total
                ? "unknown" : paid >= total ? "fullyPaid" : paid > 0m ? "partial" : "unpaid"
        };
        var sessionBalance = purchaseDetails.Sessions;
        if (sessionBalance is null && snapshot?.SessionCount is { } count)
        {
            var used = purchaseDetails.CompletedSessions.Count;
            sessionBalance = new PurchaseBalanceDto(count, used, Math.Max(0, count - used));
        }

        return new CustomerStatementDto(request.CustomerId, source.FullName,
            source.MobileNumber, source.IsActive && !source.IsBlocked, operationCount,
            snapshot?.OfferName ?? item?.Name ?? string.Empty,
            snapshot?.OfferType == OfferType.Package || item?.OfferType == "package"
                ? "package" : "session",
            new StatementPurchaseDto(purchaseDetails.Id, purchaseDetails.PurchasedAt,
                purchaseDetails.StartsOn, purchaseDetails.ExpiresOn,
                purchaseDetails.PackageStatus, purchaseDetails.PaymentStatus,
                purchaseDetails.PurchasePrice, purchaseDetails.AmountPaid,
                purchaseDetails.AmountRemaining, purchaseDetails.Pulses,
                purchaseDetails.CompletedSessions, refunded, paid - refunded,
                refunded == 0m ? "none" : refunded >= paid ? "full" : "partial"),
            sessionBalance, firstPayment, installments,
            source.IsBlocked ? "blocked" : source.IsActive ? "active" : "inactive");
    }

    private static string RecorderName(string userId, IReadOnlyDictionary<string, string> names) =>
        names.TryGetValue(userId, out var name) ? name : userId;
}
