using MediatR;

namespace Operia.Application.Customers.Queries.GetCustomerPurchaseDetails;

public sealed record GetCustomerPurchaseDetailsQuery(string PurchaseId) : IRequest<CustomerPurchaseDetailsDto>;

public sealed record PurchaseBalanceDto(int Total, int Used, int Remaining);

public sealed record PurchaseInstallmentDto(int Number, decimal Amount, string Status);

public sealed record CompletedPurchaseSessionDto(
    int SessionNumber,
    DateOnly Date,
    int StartMinutes,
    string? Performer,
    int? PulsesUsed,
    string? Notes);

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
    IReadOnlyList<CompletedPurchaseSessionDto> CompletedSessions);
