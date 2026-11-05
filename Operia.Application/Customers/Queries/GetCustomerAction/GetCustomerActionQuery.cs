using MediatR;

namespace Operia.Application.Customers.Queries.GetCustomerAction;

public sealed record GetCustomerActionQuery(string CustomerId, string PurchaseId)
    : IRequest<CustomerActionDto>;

public sealed record CustomerActionDto(
    string CustomerId, string CustomerName, string Mobile, string CustomerVersion,
    bool IsBlocked, string? BlockReason, string PurchaseId, string PurchaseStatus,
    decimal Paid, decimal Refunded, decimal RemainingRefundable);
