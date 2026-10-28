using MediatR;

namespace Operia.Application.Customers.Queries.GetCustomerActionHistory;

public sealed record GetCustomerActionHistoryQuery(string CustomerId, string PurchaseId, int Page = 1)
    : IRequest<CustomerActionHistoryPage>;

public sealed record CustomerActionHistoryRow(
    string Id, DateTime AtUtc, string Action, string? Actor,
    decimal? Amount, string? Reason);

public sealed record CustomerActionHistoryPage(
    int Total, int Page, IReadOnlyList<CustomerActionHistoryRow> Items);
