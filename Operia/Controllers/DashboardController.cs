using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Dashboard.Queries.GetDashboardSummary;

namespace Operia.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(IMediator mediator) : ControllerBase
{
    [HttpGet("summary")]
    [Authorize(Policy = Policies.DashboardRead)]
    public Task<DashboardSummaryDto> Summary(CancellationToken cancellationToken) =>
        mediator.Send(new GetDashboardSummaryQuery(), cancellationToken);
}
