namespace Operia.Application.Customers.Commands.CollectInstallment;

public sealed record CollectInstallmentRequest(string PaymentMethod, string IdempotencyKey);
