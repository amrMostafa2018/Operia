using FluentValidation;

namespace Operia.Application.Common.Validators;

public static class SubscriptionIdValidationRules
{
    public static IRuleBuilderOptions<T, string> MustNotBeEmptySubscriptionId<T>(
        this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty();
}
