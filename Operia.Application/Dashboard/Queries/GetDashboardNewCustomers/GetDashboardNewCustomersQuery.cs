using MediatR;

namespace Operia.Application.Dashboard.Queries.GetDashboardNewCustomers;

public sealed record GetDashboardNewCustomersQuery : IRequest<DashboardNewCustomersDto>;

public sealed record DashboardNewCustomersDto(int Count);
