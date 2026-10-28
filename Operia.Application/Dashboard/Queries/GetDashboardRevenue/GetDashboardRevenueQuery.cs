using MediatR;

namespace Operia.Application.Dashboard.Queries.GetDashboardRevenue;

public sealed record GetDashboardRevenueQuery : IRequest<DashboardRevenueDto>;

public sealed record DashboardRevenueDto(decimal Today, decimal Month);
