using FluentValidation;

namespace Operia.Application.Employees.Commands.ChangeEmployeeStatus;

public sealed class ChangeEmployeeStatusValidator : AbstractValidator<ChangeEmployeeStatusCommand>
{
    public ChangeEmployeeStatusValidator() => RuleFor(x => x.Id).NotEmpty();
}
