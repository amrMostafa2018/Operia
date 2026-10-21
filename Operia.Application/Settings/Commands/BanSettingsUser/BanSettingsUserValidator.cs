using FluentValidation;

namespace Operia.Application.Settings.Commands.BanSettingsUser;

public sealed class BanSettingsUserValidator : AbstractValidator<BanSettingsUserCommand>
{
    public BanSettingsUserValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty()
            .MaximumLength(450);
    }
}
