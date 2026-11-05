using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Sales.Commands.ConfirmSale;
using Operia.Domain.Entities;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Sales.Commands.CollectSale;

internal static class SaleCollectionActivation
{
    public static async Task ActivateAsync(
        IApplicationDbContext db, Sale sale, string tenantId,
        IDateTimeProvider clock, CancellationToken token)
    {
        var items = JsonSerializer.Deserialize<ConfirmedSaleItemSnapshot[]>(sale.ItemsSnapshotJson)
            ?? [];
        var direct = await db.Customers.AsNoTracking().AnyAsync(x =>
            x.TenantId == tenantId && x.Id == sale.CustomerId && x.IsDirect, token);
        if (!direct)
        {
            for (var index = 0; index < items.Length; index++)
            {
                var item = items[index];
                if (item.OfferId is null || item.PurchaseTerms is null ||
                    item.PaymentOption == "owned")
                {
                    continue;
                }
                var terms = item.PurchaseTerms;
                var total = terms.PulseCount is > 0
                    ? terms.PulseCount.Value : terms.SessionCount ?? 1;
                for (var unit = 0; unit < item.Quantity; unit++)
                {
                    var amountDue = terms.PurchasePrice - item.DueNow / item.Quantity;
                    var purchase = new CustomerPackage
                    {
                        TenantId = tenantId,
                        CustomerId = sale.CustomerId,
                        PackageId = item.OfferId,
                        SaleId = sale.Id,
                        SaleItemIndex = index,
                        SaleUnitIndex = unit,
                        PurchasePrice = terms.PurchasePrice,
                        AmountDue = amountDue,
                        ReleasedSessions = terms.PulseCount is not > 0 &&
                            terms.PaymentOption == "installment" && amountDue > 0m ? 0 : total,
                        PurchaseSnapshotJson = terms.ToJson(),
                        Total = total,
                        ReservedSessions = 0,
                        ExpiresOn = terms.PackageExpiryMonths is > 0
                            ? DateOnly.FromDateTime(clock.UtcNow)
                                .AddMonths(terms.PackageExpiryMonths.Value)
                            : null,
                        IsActive = true
                    };
                    if (terms.PaymentOption == "installment")
                    {
                        purchase.Installments.Add(new CustomerPackageInstallment
                        {
                            TenantId = tenantId,
                            CustomerPackageId = purchase.Id,
                            Number = 0,
                            Amount = terms.FirstPayment ?? item.DueNow / item.Quantity,
                            CumulativeAvailableSessions = 0,
                            CollectedAtUtc = clock.UtcNow,
                            PaymentMethod = sale.PaymentMethod
                        });
                        foreach (var row in terms.Installments.OrderBy(x => x.Number))
                        {
                            purchase.Installments.Add(new CustomerPackageInstallment
                            {
                                TenantId = tenantId,
                                CustomerPackageId = purchase.Id,
                                Number = row.Number,
                                Amount = row.Amount,
                                CumulativeAvailableSessions = row.CumulativeAvailableSessions
                            });
                        }
                    }
                    db.CustomerPackages.Add(purchase);
                }
            }
        }
        sale.Status = sale.Disposition == "Close" ? "Closed" : "ReadyToBook";
    }
}
