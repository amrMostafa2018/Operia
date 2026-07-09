using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.Commands.CompleteOnboarding;
using Operia.Application.Onboarding.Commands.ActivateSubscription;
using Operia.Application.Onboarding.Commands.AddBalancePlatform;
using Operia.Application.Onboarding.Commands.SetupBusiness;
using Operia.Application.Onboarding.DTOs;
using Operia.Application.Onboarding.Queries.GetOnboardingStatus;
using Operia.Application.Onboarding.Queries.GetSubscriptionPlans;
using Operia.Domain.Enums;
using Operia.Domain.Interfaces;
using Operia.Infrastructure.Options;

namespace Operia.Controllers;

[ApiController]
[Route("api/onboarding")]
public sealed class OnboardingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantRepository _tenantRepository;
    private readonly FileStorageSettings _fileStorageSettings;

    public OnboardingController(
        IMediator mediator,
        IFileStorageService fileStorage,
        ICurrentUserService currentUser,
        ITenantRepository tenantRepository,
        IOptions<FileStorageSettings> fileStorageSettings)
    {
        _mediator = mediator;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _tenantRepository = tenantRepository;
        _fileStorageSettings = fileStorageSettings.Value;
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
    [Consumes("multipart/form-data")]
    [HttpPost("setup-business")]
    [ProducesResponseType(typeof(SetupBusinessResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SetupBusinessResultDto>> SetupBusiness(
        [FromForm] SetupBusinessRequest request,
        IFormFile? logo,
        CancellationToken cancellationToken)
    {
        string? logoUrl = null;
        string? predeterminedTenantId = null;

        if (logo is not null)
        {
            var (tenantId, isNewTenant) = await ResolveTenantIdForUploadAsync(cancellationToken);
            if (isNewTenant)
                predeterminedTenantId = tenantId;

            logoUrl = await _fileStorage.SaveAsync(
                logo.OpenReadStream(),
                logo.FileName,
                logo.ContentType,
                tenantId,
                _fileStorageSettings.BusinessGalleriesFolder,
                cancellationToken);
        }

        var command = new SetupBusinessCommand(
            request.BusinessName,
            (BusinessType)request.BusinessType,
            request.CountryCode,
            request.City,
            request.CurrencyCode,
            logoUrl,
            predeterminedTenantId);

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

    [Authorize]
    [Consumes("multipart/form-data")]
    [HttpPost("add-balance-platform")]
    [ProducesResponseType(typeof(AddBalancePlatformResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AddBalancePlatformResultDto>> AddBalancePlatform(
        [FromForm] AddBalancePlatformRequest request,
        IFormFile? screenshot,
        CancellationToken cancellationToken)
    {
        if (screenshot is null)
        {
            throw new ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    "screenshot",
                    "Balance add request must include an Instapay screenshot.")
            ]);
        }

        var (tenantId, _) = await ResolveTenantIdForUploadAsync(cancellationToken);

        var screenShotUrl = await _fileStorage.SaveAsync(
            screenshot.OpenReadStream(),
            screenshot.FileName,
            screenshot.ContentType,
            tenantId,
            _fileStorageSettings.PlatformRevenuesFolder,
            cancellationToken);

        var command = new AddBalancePlatformCommand(request.Amount, screenShotUrl);
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [Authorize]
    [HttpPost("activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(
        [FromBody] ActivateSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    private async Task<(string TenantId, bool IsNewTenant)> ResolveTenantIdForUploadAsync(
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.TenantId))
            return (_currentUser.TenantId, false);

        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException();

        var existingTenant = await _tenantRepository.GetByOwnerUserIdWithDetailsAsync(
            userId,
            cancellationToken);

        if (existingTenant is not null)
            return (existingTenant.Id, false);

        return (Guid.NewGuid().ToString(), true);
    }
}
