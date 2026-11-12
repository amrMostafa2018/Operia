using MediatR;

namespace Operia.Application.Dashboard.Queries.GetDashboardMonthlyBookings;

public sealed record GetDashboardMonthlyBookingsQuery : IRequest<DashboardMonthlyBookingsDto>;

public sealed record DashboardMonthlyBookingsDto(int Count);
