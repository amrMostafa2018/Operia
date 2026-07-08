using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Enums;
using Operia.Domain.Interfaces;

namespace Operia.Infrastructure.Services;

public sealed class OnboardingStatusService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IPlatformRevenueRepository _platformRevenueRepository;

    public OnboardingStatusService(
        ITenantRepository tenantRepository,
        IPlatformRevenueRepository platformRevenueRepository)
    {
        _tenantRepository = tenantRepository;
        _platformRevenueRepository = platformRevenueRepository;
    }

    public async Task<OnboardingStatusDto> GetOnboardingStatusAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByOwnerUserIdForStatusAsync(userId, cancellationToken);

        if (tenant is null)
        {
            return BuildStatusDto(
                OnboardingStep.Setup, null, null, null, null, 0, 0, null, null);
        }

        var business = tenant.Businesses.FirstOrDefault();
        var businessSummary = business is null
            ? null
            : new BusinessSummaryDto(
                tenant.BusinessName,
                tenant.BusinessType,
                tenant.CountryCode,
                tenant.City,
                tenant.CurrencyCode);

        var pendingAddBalancePlatform = await MapPendingAddBalancePlatformAsync(tenant.Id, cancellationToken);
        var (usableBalance, totalBalance) = MapBalances(tenant.Balance);

        var subscription = tenant.Subscriptions
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();

        if (subscription is null)
        {
            return BuildStatusDto(
                OnboardingStep.Plan,
                tenant.Id,
                business?.Id,
                null,
                businessSummary,
                usableBalance,
                totalBalance,
                null,
                pendingAddBalancePlatform);
        }

        if (subscription.Status == SubscriptionStatus.Active)
        {
            return BuildStatusDto(
                OnboardingStep.Active,
                tenant.Id,
                business?.Id,
                subscription.Id,
                businessSummary,
                usableBalance,
                totalBalance,
                subscription.Amount,
                pendingAddBalancePlatform);
        }

        if (subscription.Status == SubscriptionStatus.Pending)
        {
            return BuildStatusDto(
                OnboardingStep.Plan,
                tenant.Id,
                business?.Id,
                subscription.Id,
                businessSummary,
                usableBalance,
                totalBalance,
                subscription.Amount,
                pendingAddBalancePlatform);
        }

        return BuildStatusDto(
            OnboardingStep.Plan,
            tenant.Id,
            business?.Id,
            subscription.Id,
            businessSummary,
            usableBalance,
            totalBalance,
            subscription.Amount,
            pendingAddBalancePlatform);
    }

    private async Task<PendingAddBalancePlatformDto?> MapPendingAddBalancePlatformAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var pending = await _platformRevenueRepository.GetLatestPendingAddBalancePlatformByTenantIdAsync(
            tenantId,
            cancellationToken);

        if (pending is null)
        {
            return null;
        }

        return new PendingAddBalancePlatformDto(
            pending.Id,
            pending.Amount,
            pending.ScreenShotUrl);
    }

    private static (decimal UsableBalance, decimal TotalBalance) MapBalances(decimal tenantBalance) =>
        (tenantBalance, tenantBalance);

    private static OnboardingStatusDto BuildStatusDto(
        OnboardingStep step,
        string? tenantId,
        string? businessId,
        string? subscriptionId,
        BusinessSummaryDto? business,
        decimal usableBalance,
        decimal totalBalance,
        decimal? subscriptionAmount,
        PendingAddBalancePlatformDto? pendingAddBalancePlatform) =>
        new(
            step,
            tenantId,
            businessId,
            subscriptionId,
            business,
            usableBalance,
            totalBalance,
            subscriptionAmount,
            pendingAddBalancePlatform);
}
