using MediatR;

namespace Operia.Application.Customers.Commands.CollectInstallment;

public sealed record CollectInstallmentCommand(
    string PurchaseId,
    string PaymentMethod,
    string IdempotencyKey) : IRequest<CollectInstallmentResult>;

public sealed record CollectInstallmentResult(
    string CollectionId,
    int InstallmentNumber,
    decimal Amount,
    decimal AmountRemaining,
    int ReleasedSessions,
    bool FullyPaid);
