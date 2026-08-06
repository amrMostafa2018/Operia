using FluentValidation;

namespace Operia.Application.Settings.Commands.UpdateSecuritySettings;

public sealed class UpdateSecuritySettingsValidator : AbstractValidator<UpdateSecuritySettingsCommand>
{
    public UpdateSecuritySettingsValidator()
    {
        RuleFor(command => command.Request)
            .NotNull();
    }
}
