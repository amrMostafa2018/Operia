using MediatR;
using Operia.Application.Customers.Queries.GetCustomerPurchaseDetails;

namespace Operia.Application.Customers.Queries.GetCustomerStatement;

public sealed record GetCustomerStatementQuery(string CustomerId, string PurchaseId)
    : IRequest<CustomerStatementDto>;

public sealed record StatementPaymentDto(
    int Number,
    decimal Amount,
    string Status,
    DateTime? PaidAtUtc,
    string? PaymentMethod,
    string? RecordedBy,
    string? Notes,
    DateOnly? DueOn = null);

public sealed record StatementFirstPaymentDto(
    decimal Amount,
    DateTime PaidAtUtc,
    string PaymentMethod,
    string? RecordedBy);

public sealed record StatementPurchaseDto(
    string Id,
    DateTime PurchasedAt,
    DateOnly? StartsOn,
    DateOnly? ExpiresOn,
    string PackageStatus,
    string PaymentStatus,
    decimal? PurchasePrice,
    decimal? AmountPaid,
    decimal? AmountRemaining,
    PurchaseBalanceDto? Pulses,
    IReadOnlyList<CompletedPurchaseSessionDto> CompletedSessions,
    decimal Refunded = 0m,
    decimal? NetCollected = null,
    string RefundStatus = "none");

public sealed record CustomerStatementDto(
    string CustomerId,
    string CustomerName,
    string Mobile,
    bool CustomerActive,
    int OperationCount,
    string ServiceName,
    string ServiceType,
    StatementPurchaseDto Purchase,
    PurchaseBalanceDto? SessionBalance,
    StatementFirstPaymentDto? FirstPayment,
    IReadOnlyList<StatementPaymentDto> Installments,
    string CustomerRestriction = "active",
    DateTime RegisteredAt = default,
    bool NextInstallmentEligible = false,
    int? NextInstallmentNumber = null,
    decimal? NextInstallmentAmount = null,
    IReadOnlyList<string>? PaymentMethods = null,
    IReadOnlyList<string>? CollectionIdempotencyKeys = null);
