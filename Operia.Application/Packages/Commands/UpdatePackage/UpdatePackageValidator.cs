using FluentValidation;
using Operia.Application.Packages;
using Operia.Application.Packages.Commands.CreatePackage;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Packages.Commands.UpdatePackage;

public sealed class UpdatePackageValidator : AbstractValidator<UpdatePackageCommand>
{
    public UpdatePackageValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Name)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Packages.PackageNameRequired)
            .MaximumLength(200).WithErrorCode(ApiErrorCodes.Packages.PackageNameMax);
        RuleFor(command => command.Description)
            .MaximumLength(250).WithErrorCode(ApiErrorCodes.Packages.PackageDescriptionMax);
        RuleFor(command => command.ServiceCategoryId)
            .NotEmpty().WithErrorCode(ApiErrorCodes.Packages.PackageCategoryRequired);
        RuleFor(command => command.SessionDurationMinutes)
            .GreaterThan(0).WithErrorCode(ApiErrorCodes.Packages.PackageDurationMin);
        RuleFor(command => command.Price)
            .GreaterThanOrEqualTo(0).WithErrorCode(ApiErrorCodes.Packages.PackagePriceMin);
        RuleFor(command => command.DiscountCode)
            .MaximumLength(50);
        RuleFor(command => command.DiscountPercent)
            .InclusiveBetween(0, 100).When(command => command.DiscountPercent.HasValue)
            .WithErrorCode(ApiErrorCodes.Packages.PackageDiscountPercentRange);
        PackageSessionPulseValidationRules.Apply(
            this,
            command => command.OfferType,
            command => command.SessionCount,
            command => command.PulseCount);
    }
}
