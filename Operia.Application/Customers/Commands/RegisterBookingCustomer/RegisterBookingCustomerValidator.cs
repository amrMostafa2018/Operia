using FluentValidation;
using Operia.Application.Common.PhoneNumbers;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Customers.Commands.RegisterBookingCustomer;

public sealed class RegisterBookingCustomerValidator : AbstractValidator<RegisterBookingCustomerCommand>
{
    public RegisterBookingCustomerValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithErrorCode(ApiErrorCodes.Auth.FullNameRequired)
            .MaximumLength(200).WithErrorCode(ApiErrorCodes.Bookings.CustomerNameTooLong);
        RuleFor(x => x.MobileNumber).NotEmpty().WithErrorCode(ApiErrorCodes.Auth.PhoneRequired)
            .Must(PhoneNumberHelper.IsValid)
            .WithErrorCode(ApiErrorCodes.Auth.PhoneInvalid);
    }
}
