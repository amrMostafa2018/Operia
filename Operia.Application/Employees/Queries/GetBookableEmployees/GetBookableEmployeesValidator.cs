using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Employees.Queries.GetBookableEmployees;

/// <summary>Validates the branch identifier used to request bookable employees.</summary>
public sealed class GetBookableEmployeesValidator : AbstractValidator<GetBookableEmployeesQuery>
{
    /// <summary>Returns bookable employees validator for the current view.</summary>
    public GetBookableEmployeesValidator()
    {
        RuleFor(query => query.BranchId)
            .NotEmpty()
            .WithErrorCode(ApiErrorCodes.Bookings.BranchRequired);
    }
}
