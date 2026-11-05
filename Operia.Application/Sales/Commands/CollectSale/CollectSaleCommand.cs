using MediatR;

namespace Operia.Application.Sales.Commands.CollectSale;

public sealed record CollectSaleCommand(
    string SaleId,
    string IdempotencyKey,
    bool PaymentConfirmed) : IRequest<CollectSaleResult>;

public sealed record CollectSaleResult(
    string SaleId,
    string CollectionId,
    bool PaymentConfirmed,
    decimal AmountCollected,
    string SaleStatus);
