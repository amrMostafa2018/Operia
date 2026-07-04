using FluentValidation;
using Operia.Application.Common.PhoneNumbers;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Auth.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Auth.PhoneRequired)
            .Must(PhoneNumberHelper.IsValid).WithErrorCode(ApiErrorCodes.Auth.PhoneInvalid);
    }
}
