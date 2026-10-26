using System.Text.Json;
using Operia.Domain.Entities;
using Operia.Domain.Enums;

namespace Operia.Application.Packages;

/// <summary>Sale terms copied from the offer. Later offer edits cannot change this record.</summary>
public sealed record PackagePurchaseSnapshot(
    string OfferName,
    OfferType OfferType,
    int SessionDurationMinutes,
    int? SessionCount,
    int? PulseCount,
    int? PackageExpiryMonths,
    string ServiceCategoryId,
    string? SubServiceCategoryId,
    string PaymentOption,
    decimal PurchasePrice,
    decimal CashListPrice,
    decimal? CashDiscountPercent,
    decimal? FirstPayment,
    IReadOnlyList<PurchaseInstallmentSnapshot> Installments)
{
    public static PackagePurchaseSnapshot Capture(Package offer, string paymentOption, decimal purchasePrice)
    {
        var installment = string.Equals(paymentOption, "installment", StringComparison.Ordinal);
        return new PackagePurchaseSnapshot(
            offer.Name,
            offer.OfferType,
            offer.SessionDurationMinutes,
            offer.SessionCount,
            offer.PulseCount,
            offer.PackageExpiryMonths,
            offer.ServiceCategoryId,
            offer.SubServiceCategoryId,
            paymentOption,
            purchasePrice,
            offer.Price,
            offer.DiscountPercent,
            installment ? offer.FirstPayment : null,
            installment
                ? offer.Installments.OrderBy(row => row.Number)
                    .Select(row => new PurchaseInstallmentSnapshot(
                        row.Number,
                        row.Amount,
                        row.SessionsUnlocked,
                        row.CumulativeAvailableSessions))
                    .ToArray()
                : []);
    }

    public string ToJson() => JsonSerializer.Serialize(this);

    public static PackagePurchaseSnapshot? FromJson(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PackagePurchaseSnapshot>(json);
}

public sealed record PurchaseInstallmentSnapshot(
    int Number,
    decimal Amount,
    int SessionsUnlocked,
    int CumulativeAvailableSessions);
