using FluentValidation;
using Operia.Application.Common.Validators;

namespace Operia.Application.Admin.Commands.ActivateSubscription;

public sealed class ActivateSubscriptionCommandValidator : AbstractValidator<ActivateSubscriptionCommand>
{
    public ActivateSubscriptionCommandValidator()
    {
        RuleFor(x => x.SubscriptionId)
            .MustNotBeEmptySubscriptionId();
    }
}
