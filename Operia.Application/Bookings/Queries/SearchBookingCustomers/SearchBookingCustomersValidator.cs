using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Queries.SearchBookingCustomers;

public sealed class SearchBookingCustomersValidator : AbstractValidator<SearchBookingCustomersQuery>
{
    public SearchBookingCustomersValidator()
    {
        RuleFor(x => x.Term).NotEmpty().MinimumLength(2).MaximumLength(100)
            .WithErrorCode(ApiErrorCodes.Bookings.CustomerSearchTermInvalid);
    }
}
