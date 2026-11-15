using MediatR;

namespace Operia.Application.Dashboard.Queries.GetDashboardSummary;

public sealed record GetDashboardSummaryQuery(string? BranchId = null) : IRequest<DashboardSummaryDto>;

public sealed record DashboardSummaryDto(
    int NewCustomersMonth,
    int TodayBookings,
    int MonthlyBookings,
    decimal TodayRevenue,
    decimal MonthlyRevenue,
    int PendingReviewCount);
