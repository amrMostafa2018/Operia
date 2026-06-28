using FluentValidation;

namespace Operia.Application.Auth.Commands.ResendRegisterOtp;

public sealed class ResendRegisterOtpCommandValidator : AbstractValidator<ResendRegisterOtpCommand>
{
    public ResendRegisterOtpCommandValidator()
    {
        RuleFor(x => x.RegistrationId).NotEmpty();
    }
}
