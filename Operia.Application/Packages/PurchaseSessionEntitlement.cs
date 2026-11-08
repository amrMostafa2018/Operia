using Operia.Domain.Entities;

namespace Operia.Application.Packages;

/// <summary>Reads the saved sale, not the mutable offer, to find bookable session positions.</summary>
public static class PurchaseSessionEntitlement
{
    public static int Released(CustomerPackage purchase)
    {
        var snapshot = PackagePurchaseSnapshot.FromJson(purchase.PurchaseSnapshotJson);
        if (snapshot?.PaymentOption == "installment")
        {
            return purchase.AmountDue == 0m
                ? purchase.Total
                : Math.Clamp(purchase.ReleasedSessions ?? 0, 0, purchase.Total);
        }

        return Math.Clamp(purchase.ReleasedSessions ?? purchase.Total, 0, purchase.Total);
    }
}
