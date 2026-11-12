using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Dashboard.Queries.GetDashboardMonthlyBookings;
using Operia.Application.Dashboard.Queries.GetDashboardNewCustomers;
using Operia.Application.Dashboard.Queries.GetDashboardRevenue;

namespace Operia.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(IMediator mediator) : ControllerBase
{
    [HttpGet("revenue")]
    [Authorize(Policy = Policies.DashboardRead)]
    public Task<DashboardRevenueDto> Revenue(CancellationToken cancellationToken) =>
        mediator.Send(new GetDashboardRevenueQuery(), cancellationToken);

    [HttpGet("new-customers")]
    [Authorize(Policy = Policies.DashboardRead)]
    public Task<DashboardNewCustomersDto> NewCustomers(CancellationToken cancellationToken) =>
        mediator.Send(new GetDashboardNewCustomersQuery(), cancellationToken);

    [HttpGet("monthly-bookings")]
    [Authorize(Policy = Policies.DashboardRead)]
    public Task<DashboardMonthlyBookingsDto> MonthlyBookings(CancellationToken cancellationToken) =>
        mediator.Send(new GetDashboardMonthlyBookingsQuery(), cancellationToken);
}
