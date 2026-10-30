using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Commands.UpdateBooking;

/// <summary>Validates input for the update booking operation.</summary>
public sealed class UpdateBookingValidator : AbstractValidator<UpdateBookingCommand>
{
    public UpdateBookingValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Version).NotNull();
        RuleFor(x => x.Items)
            .NotEmpty()
            .WithErrorCode(ApiErrorCodes.Bookings.ItemsRequired);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.PackageId).NotEmpty();
            item.RuleFor(x => x.Quantity).InclusiveBetween(1, 100);
        });
    }
}
