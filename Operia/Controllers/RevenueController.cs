using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Revenue.Queries.ListInstallmentRevenue;

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
}
