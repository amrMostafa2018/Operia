using FluentValidation;
using Operia.Application.Common.PhoneNumbers;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Auth.Commands.ResendForgotPasswordOtp;

public sealed class ResendForgotPasswordOtpCommandValidator : AbstractValidator<ResendForgotPasswordOtpCommand>
{
    public ResendForgotPasswordOtpCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Auth.PhoneRequired)
            .Must(PhoneNumberHelper.IsValid).WithErrorCode(ApiErrorCodes.Auth.PhoneInvalid);
    }
}
