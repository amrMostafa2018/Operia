using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Commands.CreateBooking;

/// <summary>Validates input for the create booking operation.</summary>
public sealed class CreateBookingValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Bookings.RequestInvalid)
            .MaximumLength(100).WithErrorCode(ApiErrorCodes.Bookings.RequestInvalid);
        RuleFor(x => x.CustomerId).NotEmpty()
            .WithErrorCode(ApiErrorCodes.Bookings.CustomerSelectionRequired)
            .When(x => !x.DirectCustomer);
        RuleFor(x => x.CustomerId).Empty().When(x => x.DirectCustomer)
            .WithErrorCode(ApiErrorCodes.Bookings.DirectCustomerSelectionInvalid);
        RuleFor(x => x.BranchId).NotEmpty().WithErrorCode(ApiErrorCodes.Bookings.BranchRequired);
        RuleFor(x => x.EmployeeId).NotEmpty().WithErrorCode(ApiErrorCodes.Bookings.RequestInvalid);
        RuleFor(x => x.StartMinutes).InclusiveBetween(0, 1439)
            .WithErrorCode(ApiErrorCodes.Bookings.RequestInvalid);
        RuleFor(x => x.EndMinutes)
            .InclusiveBetween(1, 1440).WithErrorCode(ApiErrorCodes.Bookings.RequestInvalid)
            .GreaterThan(x => x.StartMinutes).WithErrorCode(ApiErrorCodes.Bookings.RequestInvalid);
        RuleFor(x => x.Items).NotEmpty().WithErrorCode(ApiErrorCodes.Bookings.ItemsRequired);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.PackageId).NotEmpty()
                .WithErrorCode(ApiErrorCodes.Bookings.RequestInvalid);
            item.RuleFor(x => x.Quantity).InclusiveBetween(1, 100)
                .WithErrorCode(ApiErrorCodes.Bookings.RequestInvalid);
        });
    }
}
