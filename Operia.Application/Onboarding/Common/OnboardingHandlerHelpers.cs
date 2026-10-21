using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Interfaces;

namespace Operia.Application.Onboarding.Common;

internal static class OnboardingHandlerHelpers
{
    public static (DateOnly StartDate, DateOnly EndDate) ComputeActivationPeriod(
        TenantSubscription subscription,
        DateOnly today)
    {
        if (subscription.Plan?.TrialDays > 0 && subscription.Amount == 0)
        {
            return (today, today.AddDays(subscription.Plan.TrialDays));
        }

        return subscription.BillingType == BillingType.Monthly
            ? (today, today.AddMonths(1))
            : (today, today.AddYears(1));
    }

    public static bool IsPastEndDate(TenantSubscription subscription, DateOnly today) =>
        subscription.EndDate.HasValue && subscription.EndDate.Value < today;

    public static async Task<Tenant?> ResolveTenantForStatusAsync(
        ITenantRepository tenantRepository,
        string userId,
        string? currentTenantId,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(currentTenantId))
        {
            return await tenantRepository.GetByIdForStatusAsync(currentTenantId, cancellationToken);
        }

        return await tenantRepository.GetByOwnerUserIdForStatusAsync(userId, cancellationToken);
    }

    public static async Task<TenantUploadContextDto> ResolveUploadTenantContextAsync(
        ITenantRepository tenantRepository,
        string userId,
        string? currentTenantId,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(currentTenantId))
            return new TenantUploadContextDto(currentTenantId, false);

        var existingTenant = await tenantRepository.GetByOwnerUserIdWithDetailsAsync(
            userId,
            cancellationToken);

        if (existingTenant is not null)
            return new TenantUploadContextDto(existingTenant.Id, false);

        return new TenantUploadContextDto(Guid.NewGuid().ToString(), true);
    }
}
