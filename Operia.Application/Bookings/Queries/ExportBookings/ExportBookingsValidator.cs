using FluentValidation;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Queries.ExportBookings;

/// <summary>Validates input for the export bookings operation.</summary>
public sealed class ExportBookingsValidator : AbstractValidator<ExportBookingsQuery>
{
    public ExportBookingsValidator()
    {
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate)
            .WithErrorCode(ApiErrorCodes.Bookings.DateRangeInvalid);
        RuleFor(x => x.Status)
            .Must(value => string.IsNullOrWhiteSpace(value) || Enum.TryParse<BookingStatus>(value, true, out _))
            .WithErrorCode(ApiErrorCodes.Bookings.StatusInvalid);
    }
}
