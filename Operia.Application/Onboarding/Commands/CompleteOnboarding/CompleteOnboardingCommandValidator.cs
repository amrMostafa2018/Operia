using FluentValidation;
using Operia.Domain.Enums;

namespace Operia.Application.Onboarding.Commands.CompleteOnboarding;

public sealed class CompleteOnboardingCommandValidator : AbstractValidator<CompleteOnboardingCommand>
{
    public CompleteOnboardingCommandValidator()
    {
        RuleFor(x => x.PlanId)
            .NotEmpty();

        RuleFor(x => x.BillingType)
            .IsInEnum();

        RuleFor(x => x.ScreenShotUrl)
            .NotEmpty();
    }
}
