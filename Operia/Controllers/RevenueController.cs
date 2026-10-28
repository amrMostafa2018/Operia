using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Revenue.Queries.ListInstallmentRevenue;
using Operia.Application.Revenue.Queries.ListActivityRevenue;
using Operia.Application.Revenue.Queries.GetActivityRevenueSummary;
using Operia.Application.Revenue.Queries.GetRevenueBranches;

namespace Operia.Controllers;

[ApiController]
[Route("api/revenue")]
public sealed class RevenueController(IMediator mediator) : ControllerBase
{
    [HttpGet("branches")]
    [Authorize(Policy = Policies.RevenueRead)]
    public Task<IReadOnlyList<RevenueBranchDto>> Branches(CancellationToken cancellationToken) =>
        mediator.Send(new GetRevenueBranchesQuery(), cancellationToken);

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
        [FromQuery] int pageSize = 20,
        [FromQuery] string? branchId = null) =>
        mediator.Send(new ListActivityRevenueQuery(page, pageSize, branchId), cancellationToken);

    [HttpGet("summary")]
    [Authorize(Policy = Policies.RevenueRead)]
    public Task<ActivityRevenueSummaryDto> Summary(
        [FromQuery] DateTime todayFromUtc,
        [FromQuery] DateTime todayToUtc,
        [FromQuery] DateTime monthFromUtc,
        [FromQuery] DateTime monthToUtc,
        CancellationToken cancellationToken,
        [FromQuery] string? branchId = null) =>
        mediator.Send(new GetActivityRevenueSummaryQuery(
            todayFromUtc, todayToUtc, monthFromUtc, monthToUtc, branchId), cancellationToken);
}
