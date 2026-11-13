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
        RuleFor(x => x.Items)
            .Must(items => items.Count(item => item.IsUnlistedSnapshot()) <= 1)
            .WithErrorCode(ApiErrorCodes.Sales.UnlistedLimit);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.PackageId)
                .NotEmpty()
                .When(x => !x.IsUnlistedSnapshot());
            item.RuleFor(x => x.Quantity).InclusiveBetween(1, 100);
            item.RuleFor(x => x.UnlistedName)
                .NotEmpty().WithErrorCode(ApiErrorCodes.Sales.UnlistedNameInvalid)
                .MaximumLength(200).WithErrorCode(ApiErrorCodes.Sales.UnlistedNameInvalid)
                .When(x => x.IsUnlistedSnapshot());
            item.RuleFor(x => x.UnlistedPrice)
                .NotNull().WithErrorCode(ApiErrorCodes.Sales.UnlistedPriceInvalid)
                .InclusiveBetween(0m, 999999999.99m).WithErrorCode(ApiErrorCodes.Sales.UnlistedPriceInvalid)
                .When(x => x.IsUnlistedSnapshot());
            item.RuleFor(x => x.UnlistedDurationMinutes)
                .NotNull().WithErrorCode(ApiErrorCodes.Sales.UnlistedDurationInvalid)
                .InclusiveBetween(1, 1440).WithErrorCode(ApiErrorCodes.Sales.UnlistedDurationInvalid)
                .When(x => x.IsUnlistedSnapshot());
        });
    }
}
