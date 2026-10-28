using MediatR;

namespace Operia.Application.Customers.Commands.CancelAndRefundPurchase;

public sealed record CancelAndRefundPurchaseCommand(
    string PurchaseId, decimal Amount, string IdempotencyKey, string? Note)
    : IRequest<CancelAndRefundPurchaseResult>;

public sealed record CancelAndRefundPurchaseResult(
    string RefundId, decimal Amount, decimal TotalRefunded, decimal RemainingRefundable,
    string PurchaseStatus);
