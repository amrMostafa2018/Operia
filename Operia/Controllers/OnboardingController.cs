using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Common.Models;
using Operia.Application.Onboarding.Commands.ExpireTenantSubscriptionIfPastEndDate;
using Operia.Application.Onboarding.Commands.CompleteOnboarding;
using Operia.Application.Onboarding.Commands.ActivateSubscription;
using Operia.Application.Onboarding.Commands.AddBalancePlatform;
using Operia.Application.Onboarding.Commands.SetupBusiness;
using Operia.Application.Onboarding.DTOs;
using Operia.Application.Onboarding.Queries.GetOnboardingStatus;
using Operia.Application.Onboarding.Queries.GetSubscriptionPlans;
using Operia.Domain.Enums;

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

    [Authorize(Policy = Policies.AuthenticatedUser)]
    [HttpGet("status")]
    [ProducesResponseType(typeof(OnboardingStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OnboardingStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        await _mediator.Send(new ExpireTenantSubscriptionIfPastEndDateCommand(), cancellationToken);
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

    [Authorize(Policy = Policies.OnboardingManage)]
    [Consumes("multipart/form-data")]
    [HttpPost("setup-business")]
    [ProducesResponseType(typeof(SetupBusinessResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SetupBusinessResultDto>> SetupBusiness(
        [FromForm] SetupBusinessRequest request,
        IFormFile? logo,
        CancellationToken cancellationToken)
    {
        var command = new SetupBusinessCommand(
            request.BusinessName,
            (BusinessType)request.BusinessType,
            request.CountryCode,
            request.City,
            request.CurrencyCode,
            logo is null
                ? null
                : new FileUploadContent(logo.OpenReadStream(), logo.FileName, logo.ContentType));

        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [Authorize(Policy = Policies.OnboardingManage)]
    [HttpPost("complete")]
    [ProducesResponseType(typeof(OnboardingResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OnboardingResultDto>> Complete(
        [FromBody] CompleteOnboardingCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [Authorize(Policy = Policies.OnboardingManage)]
    [Consumes("multipart/form-data")]
    [HttpPost("add-balance-platform")]
    [ProducesResponseType(typeof(AddBalancePlatformResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AddBalancePlatformResultDto>> AddBalancePlatform(
        [FromForm] AddBalancePlatformRequest request,
        IFormFile? screenshot,
        CancellationToken cancellationToken)
    {
        var command = new AddBalancePlatformCommand(
            request.Amount,
            screenshot is null
                ? null
                : new FileUploadContent(screenshot.OpenReadStream(), screenshot.FileName, screenshot.ContentType));

        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [Authorize(Policy = Policies.OnboardingManage)]
    [HttpPost("activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(
        [FromBody] ActivateSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }
}
