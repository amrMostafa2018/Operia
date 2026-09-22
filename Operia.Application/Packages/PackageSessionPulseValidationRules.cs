using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Packages;

internal static class PackageSessionPulseValidationRules
{
    public static void Apply<T>(
        AbstractValidator<T> validator,
        Func<T, string> offerTypeSelector,
        Func<T, int?> sessionCountSelector,
        Func<T, int?> pulseCountSelector)
    {
        validator.When(command => IsPackageOffer(offerTypeSelector(command)), () =>
        {
            validator.RuleFor(command => command)
                .Must(command => HasSessionCount(sessionCountSelector(command)) ||
                                 HasPulseCount(pulseCountSelector(command)))
                .WithErrorCode(ApiErrorCodes.Packages.PackageSessionOrPulseRequired);

            validator.RuleFor(command => command)
                .Must(command => !(HasSessionCount(sessionCountSelector(command)) &&
                                   HasPulseCount(pulseCountSelector(command))))
                .WithErrorCode(ApiErrorCodes.Packages.PackageSessionAndPulseBothFilled);

            validator.RuleFor(command => sessionCountSelector(command))
                .GreaterThanOrEqualTo(1)
                .When(command => HasSessionCount(sessionCountSelector(command)))
                .WithErrorCode(ApiErrorCodes.Packages.PackageSessionCountMin);

            validator.RuleFor(command => pulseCountSelector(command))
                .GreaterThanOrEqualTo(1)
                .When(command => HasPulseCount(pulseCountSelector(command)))
                .WithErrorCode(ApiErrorCodes.Packages.PackagePulseCountMin);
        });
    }

    private static bool IsPackageOffer(string? offerType) =>
        string.Equals(offerType, "package", StringComparison.OrdinalIgnoreCase);

    private static bool HasSessionCount(int? sessionCount) => sessionCount is > 0;

    private static bool HasPulseCount(int? pulseCount) => pulseCount is > 0;
}
