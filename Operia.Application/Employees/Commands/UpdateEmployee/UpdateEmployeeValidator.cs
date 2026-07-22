using FluentValidation;

namespace Operia.Application.Employees.Commands.UpdateEmployee;

public sealed class UpdateEmployeeValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeValidator()
    {
        RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress(); RuleFor(x => x.MobileNumber).NotEmpty();
        RuleFor(x => x.UserName).NotEmpty(); RuleFor(x => x.BranchIds).NotEmpty();
    }
}
