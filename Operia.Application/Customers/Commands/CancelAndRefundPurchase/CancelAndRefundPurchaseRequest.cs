namespace Operia.Application.Customers.Commands.CancelAndRefundPurchase;

public sealed record CancelAndRefundPurchaseRequest(decimal Amount,
    string IdempotencyKey, string? Note);
