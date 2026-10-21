using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Queries.GetCalendarBookings;

/// <summary>Validates input for the get calendar bookings operation.</summary>
public sealed class GetCalendarBookingsValidator : AbstractValidator<GetCalendarBookingsQuery>
{
    /// <summary>Returns calendar bookings validator for the current view.</summary>
    public GetCalendarBookingsValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty()
            .WithErrorCode(ApiErrorCodes.Bookings.BranchRequired);
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate)
            .WithErrorCode(ApiErrorCodes.Bookings.DateRangeInvalid);
        RuleFor(x => x)
            .Must(x => x.ToDate.DayNumber - x.FromDate.DayNumber <= 31)
            .WithName("toDate")
            .WithErrorCode(ApiErrorCodes.Bookings.DateRangeTooLarge);
    }
}
