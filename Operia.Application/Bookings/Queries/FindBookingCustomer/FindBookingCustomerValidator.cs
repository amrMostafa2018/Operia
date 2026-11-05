using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Queries.FindBookingCustomer;

/// <summary>Validates input for the find booking customer operation.</summary>
public sealed class FindBookingCustomerValidator : AbstractValidator<FindBookingCustomerQuery>
{
    public FindBookingCustomerValidator()
    {
        RuleFor(x => x.Mobile)
            .NotEmpty()
            .WithErrorCode(ApiErrorCodes.Auth.PhoneRequired)
            .When(x => string.IsNullOrWhiteSpace(x.CustomerId));
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithErrorCode(ApiErrorCodes.Bookings.CustomerSelectionRequired)
            .When(x => string.IsNullOrWhiteSpace(x.Mobile));
    }
}
