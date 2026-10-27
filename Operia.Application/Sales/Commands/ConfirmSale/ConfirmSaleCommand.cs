using MediatR;
using Operia.Application.Sales.Queries.QuoteSale;

namespace Operia.Application.Sales.Commands.ConfirmSale;

public sealed record ConfirmSaleCommand(
    string? CustomerId,
    string BranchId,
    IReadOnlyList<QuoteSaleItem> Items,
    string? PaymentMethod,
    decimal ExpectedGrandTotal,
    decimal ExpectedDueNow,
    string IdempotencyKey,
    bool DirectCustomer = false) : IRequest<ConfirmSaleResult>;

public sealed record ConfirmSaleResult(
    string SaleId,
    string Status,
    decimal GrandTotal,
    decimal DueNow);
