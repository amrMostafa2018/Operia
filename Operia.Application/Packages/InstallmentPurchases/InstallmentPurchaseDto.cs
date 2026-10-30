namespace Operia.Application.Packages.InstallmentPurchases;

public sealed record InstallmentRowDto(
    int Number, decimal Amount, int CumulativeAvailableSessions,
    DateTime? CollectedAtUtc, string? PaymentMethod);

public sealed record InstallmentPurchaseDto(
    string Id, string PackageId, string CustomerId, decimal PurchasePrice,
    decimal AmountDue, int AvailableSessions, int TotalSessions,
    IReadOnlyList<InstallmentRowDto> Installments);
