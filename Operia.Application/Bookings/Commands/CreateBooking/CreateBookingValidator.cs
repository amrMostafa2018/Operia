using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Commands.CreateBooking;

/// <summary>Validates input for the create booking operation.</summary>
public sealed class CreateBookingValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty().WithErrorCode(ApiErrorCodes.Bookings.BranchRequired);
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.StartMinutes).InclusiveBetween(0, 1439);
        RuleFor(x => x.EndMinutes).InclusiveBetween(1, 1440).GreaterThan(x => x.StartMinutes);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.PackageId).NotEmpty();
            item.RuleFor(x => x.Quantity).InclusiveBetween(1, 100);
        });
    }
}
