using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Commands.CloseBooking;

/// <summary>Validates input shape for the close booking operation.</summary>
public sealed class CloseBookingValidator : AbstractValidator<CloseBookingCommand>
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "complete",
        "cancel"
    };

    public CloseBookingValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.BookingItemId).NotEmpty();
            item.RuleFor(x => x.Status)
                .NotEmpty()
                .Must(status => AllowedStatuses.Contains(status))
                .WithErrorCode(ApiErrorCodes.Bookings.StatusInvalid);
            item.RuleFor(x => x.Notes)
                .MaximumLength(200)
                .When(x => !string.IsNullOrWhiteSpace(x.Notes));
        });
    }
}
