using MediatR;

namespace Operia.Application.Customers.Queries.GetCustomerActionHistory;

public sealed record GetCustomerActionHistoryQuery(
    string CustomerId, string OperationId, bool SaleOnly = false, string? Cursor = null)
    : IRequest<CustomerActionHistoryPage>;

public sealed record CustomerActionHistoryRow(
    string Id, DateTime AtUtc, string Action, string? Actor,
    string? OldValue, string? NewValue, decimal? Amount, string? Reason,
    string? OldReason, string? NewReason,
    string ReferenceType, string ReferenceId);

public sealed record CustomerActionHistoryPage(
    int Total, string? NextCursor, IReadOnlyList<CustomerActionHistoryRow> Items);
