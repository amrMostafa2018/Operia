using MediatR;

namespace Operia.Application.Customers.Queries.ListDirectCustomerSales;

public sealed record ListDirectCustomerSalesQuery(int Page = 1) : IRequest<DirectCustomerSalesDto?>;

public sealed record DirectCustomerSalesDto(
    string CustomerId,
    int TotalSales,
    IReadOnlyList<DirectCustomerSaleDto> Sales);

public sealed record DirectCustomerSaleDto(
    string SaleId,
    DateTime CreatedAt,
    string? CashierId,
    string BranchId,
    decimal Amount,
    string Status,
    IReadOnlyList<string> Items);
