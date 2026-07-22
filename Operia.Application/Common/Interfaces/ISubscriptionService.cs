using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Enums;

namespace Operia.Application.Common.Interfaces;

public interface ISubscriptionService
{
    Task<IReadOnlyList<SubscriptionPlanDto>> GetActivePlansAsync(
        CancellationToken cancellationToken = default);

    Task<OnboardingResultDto> SelectPlanAsync(
        string userId,
        string planId,
        BillingType billingType,
        CancellationToken cancellationToken = default);

    Task ActivateSubscriptionAsync(
        string userId,
        string subscriptionId,
        CancellationToken cancellationToken = default);

    Task ActivateSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);
}
