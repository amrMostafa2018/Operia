using System.Text.Json;
using Operia.Application.Sales.Commands.ConfirmSale;
using Operia.Domain.Entities;

namespace Operia.Application.Sales;

public static class SaleCollectionRequirement
{
    public static bool RequiresCollection(Sale sale)
    {
        var items = JsonSerializer.Deserialize<ConfirmedSaleItemSnapshot[]>(sale.ItemsSnapshotJson)
            ?? [];
        return items.Length == 0 || items.Any(item => item.PaymentOption != "owned");
    }
}
