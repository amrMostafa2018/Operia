using FluentValidation;

namespace Operia.Application.Auth.Commands.CompleteFirstLogin;

public sealed class CompleteFirstLoginValidator : AbstractValidator<CompleteFirstLoginCommand>
{
    public CompleteFirstLoginValidator() { RuleFor(x => x.UserId).NotEmpty(); RuleFor(x => x.ResetToken).NotEmpty(); RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8); }
}
