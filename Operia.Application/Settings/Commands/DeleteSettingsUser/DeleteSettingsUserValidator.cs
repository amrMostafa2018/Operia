using FluentValidation;

namespace Operia.Application.Settings.Commands.DeleteSettingsUser;

public sealed class DeleteSettingsUserValidator : AbstractValidator<DeleteSettingsUserCommand>
{
    public DeleteSettingsUserValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty()
            .MaximumLength(450);
    }
}
