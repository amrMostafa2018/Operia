using FluentValidation;

namespace Operia.Application.Auth.Commands.ResendLoginOtp;

public sealed class ResendLoginOtpCommandValidator : AbstractValidator<ResendLoginOtpCommand>
{
    public ResendLoginOtpCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
