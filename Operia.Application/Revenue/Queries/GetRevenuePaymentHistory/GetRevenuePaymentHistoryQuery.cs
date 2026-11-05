using MediatR;

namespace Operia.Application.Revenue.Queries.GetRevenuePaymentHistory;

public sealed record GetRevenuePaymentHistoryQuery(string PaymentId)
    : IRequest<IReadOnlyList<RevenuePaymentHistoryRow>>;

public sealed record RevenuePaymentHistoryRow(
    DateTime AtUtc, string Actor, string Action, string? OldStatus, string? NewStatus);
