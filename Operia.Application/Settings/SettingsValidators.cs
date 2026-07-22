using FluentValidation;

namespace Operia.Application.Settings;

public sealed class UpdateIdentitySettingsCommandValidator : AbstractValidator<UpdateIdentitySettingsCommand>
{
    public UpdateIdentitySettingsCommandValidator()
    {
        RuleFor(x => x.Request.ActivityName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Request.Email));
        RuleFor(x => x.Request.About).MaximumLength(500).When(x => x.Request.About is not null);
        RuleFor(x => x.Request.ExistingPhotoUrls.Count + x.Request.NewPhotos.Count).LessThanOrEqualTo(6);
    }
}

public sealed class UpdateWorkingDaysCommandValidator : AbstractValidator<UpdateWorkingDaysCommand>
{
    public UpdateWorkingDaysCommandValidator()
    {
        RuleFor(x => x.Request.Days).Must(days => days.Count == 7 && days.Select(day => day.Day).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 7)
            .WithMessage("Working hours must contain seven unique days.");
        RuleForEach(x => x.Request.Days).Must(day => !day.Enabled || day.FromTime < day.ToTime)
            .WithMessage("Opening time must be before closing time.");
    }
}

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.Request.CurrentPassword).NotEmpty();
        RuleFor(x => x.Request.NewPassword).MinimumLength(8);
        RuleFor(x => x.Request.OtpCode).NotEmpty();
    }
}
