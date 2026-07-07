using Operia.Domain.Enums;

namespace Operia.Application.Onboarding.DTOs;

public sealed record OnboardingStatusDto(
    OnboardingStep Step,
    string? TenantId,
    string? BusinessId,
    string? SubscriptionId,
    BusinessSummaryDto? Business,
    decimal UsableBalance,
    decimal TotalBalance,
    decimal? SubscriptionAmount,
    PendingAddBalancePlatformDto? PendingAddBalancePlatform);

public sealed record BusinessSummaryDto(
    string BusinessName,
    BusinessType BusinessType,
    string CountryCode,
    string City,
    string CurrencyCode);
