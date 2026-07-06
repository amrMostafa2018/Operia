namespace Operia.Application.Onboarding.DTOs;

public sealed record OnboardingResultDto(
    string TenantId,
    string BusinessId,
    string SubscriptionId,
    string Status);
