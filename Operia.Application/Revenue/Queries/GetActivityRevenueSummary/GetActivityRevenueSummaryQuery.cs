using MediatR;

namespace Operia.Application.Revenue.Queries.GetActivityRevenueSummary;

public sealed record GetActivityRevenueSummaryQuery(
    DateTime TodayFromUtc,
    DateTime TodayToUtc,
    DateTime MonthFromUtc,
    DateTime MonthToUtc,
    string? BranchId) : IRequest<ActivityRevenueSummaryDto>;

public sealed record ActivityRevenueSummaryDto(
    RevenueCardDto Today,
    RevenueCardDto Month);

public sealed record RevenueCardDto(
    int TransactionCount,
    decimal GrossCollected,
    decimal Refunds,
    decimal NetCollected);
