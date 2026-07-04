using FluentValidation;
using Operia.Application.Common.PhoneNumbers;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Auth.Commands.VerifyForgotPasswordOtp;

public sealed class VerifyForgotPasswordOtpCommandValidator : AbstractValidator<VerifyForgotPasswordOtpCommand>
{
    public VerifyForgotPasswordOtpCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Auth.PhoneRequired)
            .Must(PhoneNumberHelper.IsValid).WithErrorCode(ApiErrorCodes.Auth.PhoneInvalid);

        RuleFor(x => x.OtpCode).NotEmpty();
    }
}
