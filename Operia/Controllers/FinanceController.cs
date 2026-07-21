using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Finance.DTOs;
using Operia.Application.Finance.Queries.ExportTenantSubscriptions;
using Operia.Application.Finance.Queries.GetTenantSubscriptions;
using Operia.SharedKernel.Pagination;

namespace Operia.Controllers;

[ApiController]
[Authorize]
[Route("api/finance")]
public sealed class FinanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public FinanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("subscriptions")]
    [ProducesResponseType(typeof(PagedList<TenantSubscriptionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedList<TenantSubscriptionDto>>> GetSubscriptions(
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] string? planCode,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetTenantSubscriptionsQuery(
            dateFrom,
            dateTo,
            planCode,
            status,
            pageNumber,
            pageSize);

        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("subscriptions/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportSubscriptions(
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] string? planCode,
        [FromQuery] string? status,
        CancellationToken cancellationToken = default)
    {
        var query = new ExportTenantSubscriptionsQuery(
            dateFrom,
            dateTo,
            planCode,
            status);

        var fileBytes = await _mediator.Send(query, cancellationToken);
        return File(fileBytes, "text/csv", $"operia-subscriptions-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }
}
