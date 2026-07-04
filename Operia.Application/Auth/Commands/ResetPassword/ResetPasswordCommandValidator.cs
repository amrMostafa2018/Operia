using FluentValidation;
using Operia.Application.Common.PhoneNumbers;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Auth.Commands.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Auth.PhoneRequired)
            .Must(PhoneNumberHelper.IsValid).WithErrorCode(ApiErrorCodes.Auth.PhoneInvalid);

        RuleFor(x => x.ResetToken).NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Auth.PasswordRequired)
            .MinimumLength(8).WithErrorCode(ApiErrorCodes.Auth.PasswordMinLength);

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Auth.ConfirmPasswordRequired)
            .Equal(x => x.NewPassword)
            .WithErrorCode(ApiErrorCodes.Auth.PasswordMismatch);
    }
}
