using MediatR;

namespace Operia.Application.Customers.Queries.GetCustomerPurchaseDetails;

public sealed record GetCustomerPurchaseDetailsQuery(
    string PurchaseId,
    IReadOnlyCollection<string>? AllowedBranchIds = null) : IRequest<CustomerPurchaseDetailsDto>;

public sealed record PurchaseBalanceDto(int Total, int Used, int Remaining, int Bookable = 0, int Released = 0);

public sealed record PurchaseInstallmentDto(int Number, decimal Amount, string Status);

public sealed record PurchaseCollectionDto(
    string Id, int InstallmentNumber, decimal Amount, string PaymentMethod,
    DateTime ConfirmedAtUtc, string IdempotencyKey, string? RecordedBy = null);

public sealed record CompletedPurchaseSessionDto(
    int SessionNumber,
    DateOnly Date,
    int StartMinutes,
    string? Performer,
    int? PulsesUsed,
    string? Notes,
    string? SessionType = null);

public sealed record CustomerPurchaseDetailsDto(
    string Id,
    DateTime PurchasedAt,
    DateOnly? StartsOn,
    DateOnly? ExpiresOn,
    int? ValidityMonths,
    string PackageStatus,
    string PaymentStatus,
    PurchaseBalanceDto? Sessions,
    PurchaseBalanceDto? Pulses,
    decimal? PurchasePrice,
    decimal? AmountPaid,
    decimal? AmountRemaining,
    int? NextInstallmentNumber,
    int InstallmentCount,
    IReadOnlyList<PurchaseInstallmentDto> Installments,
    IReadOnlyList<CompletedPurchaseSessionDto> CompletedSessions,
    bool NextInstallmentEligible,
    decimal? NextInstallmentAmount,
    IReadOnlyList<string> PaymentMethods,
    IReadOnlyList<PurchaseCollectionDto> Collections);
