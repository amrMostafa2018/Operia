using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Admin.Commands.ActivateSubscription;
using Operia.Application.Admin.Commands.ApproveAddBalancePlatform;
using Operia.Application.Auth;

namespace Operia.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = Policies.Platform.Manage)]
public sealed class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("add-balance-platform/{revenueId}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ApproveAddBalancePlatform(
        string revenueId,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new ApproveAddBalancePlatformCommand(revenueId), cancellationToken);
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
