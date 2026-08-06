using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Settings;
using Operia.Application.Settings.Commands.UpdateIdentitySettings;
using Operia.Application.Settings.Queries.GetIdentitySettings;
using Operia.Contracts.Settings.UpdateIdentitySettings;

namespace Operia.Controllers;

[ApiController]
[Authorize(Policy = Policies.SettingsManage)]
[Route("api/settings/identity")]
public sealed class SettingsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IdentitySettingsDto>> Get(CancellationToken cancellationToken)
    {
        var settings = await mediator.Send(new GetIdentitySettingsQuery(), cancellationToken);
        return Ok(settings);
    }

    [HttpPut]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<IdentitySettingsDto>> Update(
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
}
