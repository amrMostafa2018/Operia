using FluentValidation;

namespace Operia.Application.Auth.Commands.VerifyRegisterOtp;

public sealed class VerifyRegisterOtpCommandValidator : AbstractValidator<VerifyRegisterOtpCommand>
{
    public VerifyRegisterOtpCommandValidator()
    {
        RuleFor(x => x.RegistrationId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}
