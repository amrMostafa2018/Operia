using FluentValidation;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Onboarding.Commands.SetupBusiness;

public sealed class SetupBusinessCommandValidator : AbstractValidator<SetupBusinessCommand>
{
    public SetupBusinessCommandValidator()
    {
        RuleFor(x => x.BusinessName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(200);

        RuleFor(x => x.BusinessType)
            .IsInEnum();

        RuleFor(x => x.CountryCode)
            .NotEmpty()
            .Length(2);

        RuleFor(x => x.City)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.CurrencyCode)
            .NotEmpty()
            .Length(3);
    }
}
