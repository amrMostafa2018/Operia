using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Enums;

namespace Operia.Application.Common.Interfaces;

public interface IOnboardingService
{
    Task<SetupBusinessResultDto> SetupBusinessAsync(
        string userId,
        string businessName,
        BusinessType businessType,
        string countryCode,
        string city,
        string currencyCode,
        string? logoUrl,
        CancellationToken cancellationToken = default);

    Task<OnboardingResultDto> CompleteOnboardingAsync(
        string userId,
        string planId,
        BillingType billingType,
        string screenShotUrl,
        CancellationToken cancellationToken = default);

    Task<OnboardingStatusDto> GetOnboardingStatusAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionPlanDto>> GetSubscriptionPlansAsync(
        CancellationToken cancellationToken = default);
}
