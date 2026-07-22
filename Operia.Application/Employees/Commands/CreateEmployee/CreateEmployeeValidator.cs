using FluentValidation;

namespace Operia.Application.Employees.Commands.CreateEmployee;

public sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.MobileNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.TemporaryPassword).NotEmpty().MinimumLength(8);
        RuleFor(x => x.BranchIds).NotEmpty();
        RuleFor(x => x.Role).Must(x => new[] { "SuperAdmin", "Admin", "Reception", "Staff" }.Contains(x));
    }
}
