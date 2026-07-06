using FluentValidation;

namespace Operia.Application.Admin.Commands.ActivateSubscription;

public sealed class ActivateSubscriptionCommandValidator : AbstractValidator<ActivateSubscriptionCommand>
{
    public ActivateSubscriptionCommandValidator()
    {
        RuleFor(x => x.SubscriptionId)
            .NotEmpty();
    }
}
