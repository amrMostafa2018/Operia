using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Dashboard.Queries.GetDashboardRevenue;

namespace Operia.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(IMediator mediator) : ControllerBase
{
    [HttpGet("revenue")]
    [Authorize(Policy = Policies.DashboardRead)]
    public Task<DashboardRevenueDto> Revenue(
        [FromQuery] DateTime todayFromUtc,
        [FromQuery] DateTime todayToUtc,
        [FromQuery] DateTime monthFromUtc,
        [FromQuery] DateTime monthToUtc,
        CancellationToken cancellationToken) =>
        mediator.Send(new GetDashboardRevenueQuery(
            todayFromUtc, todayToUtc, monthFromUtc, monthToUtc), cancellationToken);
}
