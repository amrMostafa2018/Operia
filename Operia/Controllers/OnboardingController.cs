using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Onboarding.Commands.CompleteOnboarding;
using Operia.Application.Onboarding.Commands.SetupBusiness;
using Operia.Application.Onboarding.DTOs;
using Operia.Application.Onboarding.Queries.GetOnboardingStatus;
using Operia.Application.Onboarding.Queries.GetSubscriptionPlans;

namespace Operia.Controllers;

[ApiController]
[Route("api/onboarding")]
public sealed class OnboardingController : ControllerBase
{
    private readonly IMediator _mediator;

    public OnboardingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Authorize]
    [HttpGet("status")]
    [ProducesResponseType(typeof(OnboardingStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OnboardingStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetOnboardingStatusQuery(), cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("plans")]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionPlanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionPlanDto>>> GetPlans(
        CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetSubscriptionPlansQuery(), cancellationToken));
    }

    [Authorize]
    [HttpPost("setup-business")]
    [ProducesResponseType(typeof(SetupBusinessResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SetupBusinessResultDto>> SetupBusiness(
        [FromBody] SetupBusinessCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [Authorize]
    [HttpPost("complete")]
    [ProducesResponseType(typeof(OnboardingResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OnboardingResultDto>> Complete(
        [FromBody] CompleteOnboardingCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(command, cancellationToken));
    }
}
