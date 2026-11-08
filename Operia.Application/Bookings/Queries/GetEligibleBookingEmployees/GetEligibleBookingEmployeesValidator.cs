using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Queries.GetEligibleBookingEmployees;

public sealed class GetEligibleBookingEmployeesValidator : AbstractValidator<GetEligibleBookingEmployeesQuery>
{
    public GetEligibleBookingEmployeesValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty().WithErrorCode(ApiErrorCodes.Bookings.BranchRequired);
        RuleFor(x => x.StartMinutes).InclusiveBetween(0, 1439)
            .WithErrorCode(ApiErrorCodes.Bookings.RequestInvalid);
        RuleFor(x => x.EndMinutes).InclusiveBetween(1, 1440)
            .GreaterThan(x => x.StartMinutes)
            .WithErrorCode(ApiErrorCodes.Bookings.RequestInvalid);
        RuleFor(x => x.Items).NotNull().WithErrorCode(ApiErrorCodes.Bookings.ItemsRequired);
        RuleFor(x => x.UnlistedDurationMinutes).InclusiveBetween(0, 1440)
            .WithErrorCode(ApiErrorCodes.Bookings.RequestInvalid);
        RuleFor(x => x).Must(x => (x.Items?.Count ?? 0) > 0 || x.UnlistedDurationMinutes > 0)
            .WithErrorCode(ApiErrorCodes.Bookings.ItemsRequired);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.PackageId).NotEmpty()
                .WithErrorCode(ApiErrorCodes.Bookings.RequestInvalid);
            item.RuleFor(x => x.Quantity).InclusiveBetween(1, 100)
                .WithErrorCode(ApiErrorCodes.Bookings.RequestInvalid);
        });
    }
}
