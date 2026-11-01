using Operia.Domain.Entities;

namespace Operia.Application.Packages.InstallmentPurchases;

internal static class InstallmentPurchaseMapper
{
    public static InstallmentPurchaseDto ToDto(CustomerPackage purchase)
    {
        var rows = purchase.Installments.OrderBy(x => x.Number).ToList();
        return new InstallmentPurchaseDto(
            purchase.Id, purchase.PackageId, purchase.CustomerId,
            purchase.PurchasePrice ?? 0, purchase.AmountDue ?? 0,
            purchase.Total, rows.Last().CumulativeAvailableSessions,
            rows.Select(x => new InstallmentRowDto(
                x.Number, x.Amount, x.CumulativeAvailableSessions,
                x.CollectedAtUtc, x.PaymentMethod)).ToList());
    }
}
