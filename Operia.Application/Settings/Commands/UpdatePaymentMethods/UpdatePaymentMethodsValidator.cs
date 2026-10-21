using FluentValidation;

namespace Operia.Application.Settings.Commands.UpdatePaymentMethods;

public sealed class UpdatePaymentMethodsValidator : AbstractValidator<UpdatePaymentMethodsCommand>
{
    public UpdatePaymentMethodsValidator()
    {
        RuleFor(command => command.Request)
            .NotNull();
    }
}
