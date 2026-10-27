using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Revenue.Queries.ListInstallmentRevenue;
using Operia.Application.Revenue.Queries.ListActivityRevenue;

namespace Operia.Controllers;

[ApiController]
[Route("api/revenue")]
public sealed class RevenueController(IMediator mediator) : ControllerBase
{
    [HttpGet("installment-collections")]
    [Authorize(Policy = Policies.RevenueRead)]
    public Task<InstallmentRevenuePage> InstallmentCollections(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        mediator.Send(new ListInstallmentRevenueQuery(page, pageSize), cancellationToken);

    [HttpGet("collections")]
    [Authorize(Policy = Policies.RevenueRead)]
    public Task<ActivityRevenuePage> Collections(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        mediator.Send(new ListActivityRevenueQuery(page, pageSize), cancellationToken);
}
