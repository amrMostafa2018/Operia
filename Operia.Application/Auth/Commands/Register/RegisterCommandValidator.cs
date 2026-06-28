using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Auth.Commands.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Auth.EmailRequired)
            .EmailAddress().WithErrorCode(ApiErrorCodes.Auth.EmailInvalid);

        RuleFor(x => x.Password)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Auth.PasswordRequired)
            .MinimumLength(8).WithErrorCode(ApiErrorCodes.Auth.PasswordMinLength);

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Auth.ConfirmPasswordRequired)
            .Equal(x => x.Password)
            .WithErrorCode(ApiErrorCodes.Auth.PasswordMismatch);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Auth.PhoneRequired)
            .MinimumLength(10).WithErrorCode(ApiErrorCodes.Auth.PhoneInvalid);
    }
}
