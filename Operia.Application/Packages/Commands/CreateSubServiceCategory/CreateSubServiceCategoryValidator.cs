using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Packages.Commands.CreateSubServiceCategory;

public sealed class CreateSubServiceCategoryValidator : AbstractValidator<CreateSubServiceCategoryCommand>
{
    public CreateSubServiceCategoryValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Packages.SubCategoryNameRequired)
            .MaximumLength(200).WithErrorCode(ApiErrorCodes.Packages.SubCategoryNameMax);
        RuleFor(command => command.ServiceCategoryId)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Packages.SubCategoryParentRequired);
    }
}
