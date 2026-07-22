using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Common.Authorization;
using Operia.Application.Settings;

namespace Operia.Controllers;

[ApiController]
[Authorize(Policy = Permissions.Admin.SettingsRead)]
[Route("api/settings/identity")]
public sealed class SettingsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IdentitySettingsDto>> Get(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetIdentitySettingsQuery(), cancellationToken));

    [HttpPut]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<IdentitySettingsDto>> Update([FromForm] UpdateIdentityForm request, CancellationToken cancellationToken)
    {
        var photos = new List<SettingsImageUpload>();
        if (request.PrimaryPhoto is not null) photos.Add(new(request.PrimaryPhoto.OpenReadStream(), request.PrimaryPhoto.FileName, request.PrimaryPhoto.ContentType, true));
        foreach (var photo in request.AdditionalPhotos ?? []) photos.Add(new(photo.OpenReadStream(), photo.FileName, photo.ContentType, false));
        var command = new UpdateIdentitySettingsCommand(new(request.ActivityName, request.ContactPhone, request.WhatsappPhone, request.Email, request.MainAddress, request.About, request.ExistingPhotoUrls ?? [], photos));
        return Ok(await mediator.Send(command, cancellationToken));
    }
}

public sealed class UpdateIdentityForm
{
    public string ActivityName { get; init; } = string.Empty;
    public string? ContactPhone { get; init; }
    public string? WhatsappPhone { get; init; }
    public string? Email { get; init; }
    public string? MainAddress { get; init; }
    public string? About { get; init; }
    public List<string>? ExistingPhotoUrls { get; init; }
    public IFormFile? PrimaryPhoto { get; init; }
    public List<IFormFile>? AdditionalPhotos { get; init; }
}
