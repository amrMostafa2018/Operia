using FluentValidation;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.PhoneNumbers;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Auth.Commands.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator(IIdentityService identityService)
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Auth.FullNameRequired)
            .MinimumLength(3).WithErrorCode(ApiErrorCodes.Auth.FullNameMinLength);

        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Auth.EmailRequired)
            .EmailAddress().WithErrorCode(ApiErrorCodes.Auth.EmailInvalid)
            .MustAsync(async (email, cancellationToken) =>
                !await identityService.IsEmailRegisteredAsync(email, cancellationToken))
            .WithErrorCode(ApiErrorCodes.Auth.EmailAlreadyRegistered);

        RuleFor(x => x.Password)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Auth.PasswordRequired)
            .MinimumLength(8).WithErrorCode(ApiErrorCodes.Auth.PasswordMinLength);

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Auth.ConfirmPasswordRequired)
            .Equal(x => x.Password)
            .WithErrorCode(ApiErrorCodes.Auth.PasswordMismatch);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Auth.PhoneRequired)
            .Must(PhoneNumberHelper.IsValid).WithErrorCode(ApiErrorCodes.Auth.PhoneInvalid)
            .MustAsync(async (phoneNumber, cancellationToken) =>
                !await identityService.IsPhoneRegisteredAsync(
                    PhoneNumberHelper.ToE164(phoneNumber),
                    cancellationToken))
            .WithErrorCode(ApiErrorCodes.Auth.PhoneAlreadyRegistered);
    }
}
