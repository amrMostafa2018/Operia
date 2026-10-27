using MediatR;

namespace Operia.Application.Dashboard.Queries.GetDashboardRevenue;

public sealed record GetDashboardRevenueQuery(
    DateTime TodayFromUtc,
    DateTime TodayToUtc,
    DateTime MonthFromUtc,
    DateTime MonthToUtc) : IRequest<DashboardRevenueDto>;

public sealed record DashboardRevenueDto(decimal Today, decimal Month);
