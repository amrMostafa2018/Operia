using FluentValidation;

namespace Operia.Application.Bookings.Commands.CancelBooking;

/// <summary>Validates input for the cancel booking operation.</summary>
public sealed class CancelBookingValidator : AbstractValidator<CancelBookingCommand>
{
    public CancelBookingValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
    }
}
