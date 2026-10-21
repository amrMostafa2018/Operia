using FluentValidation;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Queries.ListBookings;

/// <summary>Validates input for the list bookings operation.</summary>
public sealed class ListBookingsValidator : AbstractValidator<ListBookingsQuery>
{
    private static readonly int[] PageSizes = [5, 10, 20, 25, 50, 100, 500, 1000, 2000];

    public ListBookingsValidator()
    {
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate)
            .WithErrorCode(ApiErrorCodes.Bookings.DateRangeInvalid);
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).Must(PageSizes.Contains);
        RuleFor(x => x.Status)
            .Must(value => string.IsNullOrWhiteSpace(value) ||
                           Enum.TryParse<BookingStatus>(value, true, out _))
            .WithErrorCode(ApiErrorCodes.Bookings.StatusInvalid);
    }
}
