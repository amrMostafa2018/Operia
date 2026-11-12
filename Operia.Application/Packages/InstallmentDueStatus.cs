using Operia.Domain.Entities;

namespace Operia.Application.Packages;

/// <summary>
/// Classifies the next unpaid installment the same way customer operations does.
/// "due" is the installment that must be collected before the purchase can be booked again.
/// </summary>
public static class InstallmentDueStatus
{
    public const string None = "none";
    public const string Paid = "paid";
    public const string Due = "due";
    public const string Scheduled = "scheduled";

    public static string Resolve(
        PackagePurchaseSnapshot? snapshot,
        decimal remaining,
        decimal collected,
        int used)
    {
        if (snapshot?.PaymentOption != "installment")
        {
            return None;
        }

        if (remaining == 0m)
        {
            return Paid;
        }

        var ordered = snapshot.Installments.OrderBy(x => x.Number).ToArray();
        var cumulative = snapshot.FirstPayment ?? 0m;
        for (var index = 0; index < ordered.Length; index++)
        {
            cumulative += ordered[index].Amount;
            if (collected < cumulative)
            {
                return index == 0 || used >= ordered[index - 1].CumulativeAvailableSessions
                    ? Due
                    : Scheduled;
            }
        }

        return Due;
    }

    /// <summary>True when the next installment is due now, including a purchase with released sessions left.</summary>
    public static bool IsDueNow(CustomerPackage purchase)
    {
        var snapshot = PackagePurchaseSnapshot.FromJson(purchase.PurchaseSnapshotJson);
        if (snapshot is null)
        {
            return false;
        }

        var price = purchase.PurchasePrice ?? snapshot.PurchasePrice;
        var remaining = purchase.AmountDue ?? 0m;
        var collected = Math.Clamp(price - remaining, 0m, price);
        return Resolve(snapshot, remaining, collected, purchase.Used) == Due;
    }
}
