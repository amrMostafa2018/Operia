using FluentValidation;

namespace Operia.Application.Settings.Commands.ChangePassword;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.Request.CurrentPassword).NotEmpty();
        RuleFor(x => x.Request.NewPassword).MinimumLength(8);
        RuleFor(x => x.Request.OtpCode).NotEmpty();
    }
}
