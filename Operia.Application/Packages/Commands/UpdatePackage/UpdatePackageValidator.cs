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
        RuleFor(command => command.Price)
            .GreaterThan(0).When(command => command.CashEnabled)
            .WithErrorCode(ApiErrorCodes.Packages.PackageCashPricePositive);
        RuleFor(command => command.CashEnabled)
            .Must((command, cashEnabled) => cashEnabled || command.InstallmentEnabled)
            .WithErrorCode(ApiErrorCodes.Packages.PackagePaymentOptionRequired);
        RuleFor(command => command.InstallmentPrice)
            .NotNull().When(command => command.InstallmentEnabled)
            .WithErrorCode(ApiErrorCodes.Packages.PackageInstallmentPriceRequired);
        RuleFor(command => command.InstallmentPrice)
            .GreaterThan(0).When(command => command.InstallmentEnabled && command.InstallmentPrice.HasValue)
            .WithErrorCode(ApiErrorCodes.Packages.PackageInstallmentPriceMin);
        RuleFor(command => command.InstallmentPrice)
            .GreaterThanOrEqualTo(0).When(command => !command.InstallmentEnabled && command.InstallmentPrice.HasValue)
            .WithErrorCode(ApiErrorCodes.Packages.PackageInstallmentPriceMin);
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
