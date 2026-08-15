using FluentValidation.Results;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.PhoneNumbers;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Employees.Common;

internal static class EmployeeIdentityUniquenessHelper
{
    public static async Task EnsureUniqueAsync(
        IIdentityService identityService,
        string? ignoreUserId,
        string userName,
        string email,
        string mobileNumber,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<ValidationFailure>();

        if (!string.IsNullOrWhiteSpace(email)
            && await identityService.IsEmailRegisteredAsync(email, ignoreUserId, cancellationToken))
        {
            failures.Add(ValidationFailureFactory.Create("email", ApiErrorCodes.Auth.EmailAlreadyRegistered));
        }

        if (!string.IsNullOrWhiteSpace(userName)
            && await identityService.IsUserNameRegisteredAsync(userName, ignoreUserId, cancellationToken))
        {
            failures.Add(ValidationFailureFactory.Create("userName", ApiErrorCodes.Auth.UserNameAlreadyRegistered));
        }

        if (!string.IsNullOrWhiteSpace(mobileNumber) && PhoneNumberHelper.IsValid(mobileNumber))
        {
            var normalizedMobile = PhoneNumberHelper.ToE164(mobileNumber);
            if (await identityService.IsPhoneRegisteredAsync(normalizedMobile, ignoreUserId, cancellationToken))
            {
                failures.Add(ValidationFailureFactory.Create("mobileNumber", ApiErrorCodes.Auth.PhoneAlreadyRegistered));
            }
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);
    }
}
