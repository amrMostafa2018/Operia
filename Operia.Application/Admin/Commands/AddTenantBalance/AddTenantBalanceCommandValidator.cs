using FluentValidation;

namespace Operia.Application.Admin.Commands.AddTenantBalance;

public sealed class AddTenantBalanceCommandValidator : AbstractValidator<AddTenantBalanceCommand>
{
    public AddTenantBalanceCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty();

        RuleFor(x => x.Amount)
            .GreaterThan(0);
    }
}
