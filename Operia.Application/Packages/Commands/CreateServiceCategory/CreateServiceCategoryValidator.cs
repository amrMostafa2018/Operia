using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Packages.Commands.CreateServiceCategory;

public sealed class CreateServiceCategoryValidator : AbstractValidator<CreateServiceCategoryCommand>
{
    public CreateServiceCategoryValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Packages.CategoryNameRequired)
            .MaximumLength(200).WithErrorCode(ApiErrorCodes.Packages.CategoryNameMax);
        RuleFor(command => command.Icon)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Packages.CategoryIconRequired)
            .MaximumLength(100);
    }
}
