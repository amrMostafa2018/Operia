using MediatR;

namespace Operia.Application.Customers.Queries.GetCustomerAction;

public sealed record GetCustomerSaleActionQuery(string CustomerId, string SaleId)
    : IRequest<CustomerBlockActionDto>;

public sealed record CustomerBlockActionDto(
    string CustomerId, string CustomerName, string Mobile, string CustomerVersion,
    bool IsBlocked, string? BlockReason);
