using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Queries.SearchBookingCustomers;

public sealed class SearchBookingCustomersValidator : AbstractValidator<SearchBookingCustomersQuery>
{
    public SearchBookingCustomersValidator()
    {
        RuleFor(x => x.Term)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Bookings.CustomerSearchTermInvalid)
            .MinimumLength(2).WithErrorCode(ApiErrorCodes.Bookings.CustomerSearchTermInvalid)
            .MaximumLength(100).WithErrorCode(ApiErrorCodes.Bookings.CustomerSearchTermInvalid);
    }
}
