using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Settings;
using Operia.Application.Settings.Commands.BanSettingsUser;
using Operia.Application.Settings.Commands.ChangePassword;
using Operia.Application.Settings.Commands.DeactivateAccount;
using Operia.Application.Settings.Commands.DeleteSettingsUser;
using Operia.Application.Settings.Commands.SendPasswordOtp;
using Operia.Application.Settings.Commands.UpdateIdentitySettings;
using Operia.Application.Settings.Commands.UpdatePaymentMethods;
using Operia.Application.Settings.Commands.UpdateSecuritySettings;
using Operia.Application.Settings.Commands.UpdateWorkingDays;
using Operia.Application.Settings.Queries.GetIdentitySettings;
using Operia.Application.Settings.Queries.GetPaymentMethods;
using Operia.Application.Settings.Queries.GetSecuritySettings;
using Operia.Application.Settings.Queries.GetWorkingDays;
using Operia.Contracts.Settings.UpdateIdentitySettings;

namespace Operia.Controllers;

[ApiController]
[Route("api/settings")]
public sealed class SettingsController(IMediator mediator) : ControllerBase
{
    [HttpGet("identity")]
    [Authorize(Policy = Policies.SettingsIdentityRead)]
    public async Task<ActionResult<IdentitySettingsDto>> GetIdentity(CancellationToken cancellationToken)
    {
        var settings = await mediator.Send(new GetIdentitySettingsQuery(), cancellationToken);
        return Ok(settings);
    }

    [HttpPut("identity")]
    [Authorize(Policy = Policies.SettingsIdentityManage)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<IdentitySettingsDto>> UpdateIdentity(
        [FromForm] UpdateIdentityForm request,
        CancellationToken cancellationToken)
    {
        var photos = new List<SettingsImageUpload>();
        if (request.PrimaryPhoto is not null)
        {
            photos.Add(new SettingsImageUpload(
                request.PrimaryPhoto.OpenReadStream(),
                request.PrimaryPhoto.FileName,
                request.PrimaryPhoto.ContentType,
                true));
        }

        foreach (var photo in request.AdditionalPhotos ?? [])
        {
            photos.Add(new SettingsImageUpload(
                photo.OpenReadStream(),
                photo.FileName,
                photo.ContentType,
                false));
        }

        var updateRequest = new UpdateIdentitySettingsRequest(
            request.ActivityName,
            request.ContactPhone,
            request.WhatsappPhone,
            request.Email,
            request.MainAddress,
            request.About,
            request.ExistingPhotoUrls ?? [],
            photos);

        var settings = await mediator.Send(
            new UpdateIdentitySettingsCommand(updateRequest),
            cancellationToken);

        return Ok(settings);
    }

    [HttpGet("payment-methods")]
    [Authorize(Policy = Policies.SettingsPaymentsRead)]
    public async Task<ActionResult<PaymentMethodsDto>> GetPaymentMethods(
        CancellationToken cancellationToken)
    {
        var paymentMethods = await mediator.Send(new GetPaymentMethodsQuery(), cancellationToken);
        return Ok(paymentMethods);
    }

    [HttpPut("payment-methods")]
    [Authorize(Policy = Policies.SettingsPaymentsManage)]
    public async Task<ActionResult<PaymentMethodsDto>> UpdatePaymentMethods(
        [FromBody] PaymentMethodsDto request,
        CancellationToken cancellationToken)
    {
        var paymentMethods = await mediator.Send(
            new UpdatePaymentMethodsCommand(request),
            cancellationToken);

        return Ok(paymentMethods);
    }

    [HttpGet("working-days")]
    [Authorize(Policy = Policies.SettingsWorkingDaysRead)]
    public async Task<ActionResult<WorkingDaysSettingsDto>> GetWorkingDays(
        CancellationToken cancellationToken)
    {
        var workingDays = await mediator.Send(new GetWorkingDaysQuery(), cancellationToken);
        return Ok(workingDays);
    }

    [HttpPut("working-days")]
    [Authorize(Policy = Policies.SettingsWorkingDaysManage)]
    public async Task<ActionResult<WorkingDaysSettingsDto>> UpdateWorkingDays(
        [FromBody] WorkingDaysSettingsDto request,
        CancellationToken cancellationToken)
    {
        var workingDays = await mediator.Send(
            new UpdateWorkingDaysCommand(request),
            cancellationToken);

        return Ok(workingDays);
    }

    [HttpGet("security")]
    [Authorize(Policy = Policies.SettingsSecurityRead)]
    public async Task<ActionResult<SecuritySettingsDto>> GetSecurity(CancellationToken cancellationToken)
    {
        var settings = await mediator.Send(new GetSecuritySettingsQuery(), cancellationToken);
        return Ok(settings);
    }

    [HttpPut("security")]
    [Authorize(Policy = Policies.SettingsSecurityManage)]
    public async Task<IActionResult> UpdateSecurity(
        [FromBody] UpdateSecurityRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateSecuritySettingsCommand(request), cancellationToken);
        return NoContent();
    }

    [HttpPost("security/password-otp")]
    [Authorize(Policy = Policies.SettingsPasswordChange)]
    public async Task<IActionResult> SendPasswordOtp(CancellationToken cancellationToken)
    {
        await mediator.Send(new SendPasswordOtpCommand(), cancellationToken);
        return NoContent();
    }

    [HttpPost("security/change-password")]
    [Authorize(Policy = Policies.SettingsPasswordChange)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new ChangePasswordCommand(request), cancellationToken);
        return NoContent();
    }

    [HttpPost("security/users/{userId}/ban")]
    [Authorize(Policy = Policies.SettingsUsersBan)]
    public async Task<IActionResult> BanUser(string userId, CancellationToken cancellationToken)
    {
        await mediator.Send(new BanSettingsUserCommand(userId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("security/users/{userId}")]
    [Authorize(Policy = Policies.SettingsUsersDelete)]
    public async Task<IActionResult> DeleteUser(string userId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteSettingsUserCommand(userId), cancellationToken);
        return NoContent();
    }

    [HttpPost("security/deactivate-account")]
    [Authorize(Policy = Policies.SettingsAccountDeactivate)]
    public async Task<IActionResult> Deactivate(CancellationToken cancellationToken)
    {
        await mediator.Send(new DeactivateAccountCommand(), cancellationToken);
        return NoContent();
    }
}
