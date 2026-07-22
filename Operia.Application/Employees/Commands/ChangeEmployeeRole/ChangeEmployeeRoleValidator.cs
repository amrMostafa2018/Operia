using FluentValidation;

namespace Operia.Application.Employees.Commands.ChangeEmployeeRole;

public sealed class ChangeEmployeeRoleValidator : AbstractValidator<ChangeEmployeeRoleCommand>
{
    public ChangeEmployeeRoleValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Role).NotEmpty(); }
}
