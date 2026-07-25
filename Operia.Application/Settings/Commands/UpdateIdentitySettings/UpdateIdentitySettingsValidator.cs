using FluentValidation;

namespace Operia.Application.Settings.Commands.UpdateIdentitySettings;

public sealed class UpdateIdentitySettingsValidator : AbstractValidator<UpdateIdentitySettingsCommand>
{
    public UpdateIdentitySettingsValidator()
    {
        RuleFor(x => x.Request.ActivityName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Request.Email));
        RuleFor(x => x.Request.About).MaximumLength(500).When(x => x.Request.About is not null);
        RuleFor(x => x.Request.ExistingPhotoUrls.Count + x.Request.NewPhotos.Count).LessThanOrEqualTo(6);
    }
}
