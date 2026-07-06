using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Admin.Commands.ActivateSubscription;
using Operia.Application.Admin.Commands.AddTenantBalance;
using Operia.Application.Common.Authorization;

namespace Operia.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("tenants/{tenantId}/add-balance")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddTenantBalance(
        string tenantId,
        [FromBody] AddTenantBalanceRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new AddTenantBalanceCommand(tenantId, request.Amount), cancellationToken);
        return NoContent();
    }

    [HttpPost("subscriptions/{subscriptionId}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ActivateSubscription(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new ActivateSubscriptionCommand(subscriptionId), cancellationToken);
        return NoContent();
    }
}

public sealed record AddTenantBalanceRequest(decimal Amount);
