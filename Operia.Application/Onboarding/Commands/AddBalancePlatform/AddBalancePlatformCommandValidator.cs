using FluentValidation;

namespace Operia.Application.Onboarding.Commands.AddBalancePlatform;

public sealed class AddBalancePlatformCommandValidator
    : AbstractValidator<AddBalancePlatformCommand>
{
    public AddBalancePlatformCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.ScreenShotUrl).NotEmpty();
    }
}
