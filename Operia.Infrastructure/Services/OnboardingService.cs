using System.Text.Json;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Entities;
using Operia.Domain.Exceptions;
using Operia.Domain.Interfaces;

namespace Operia.Infrastructure.Services;

public sealed class OnboardingService : IOnboardingService
{
    private readonly OnboardingStatusService _onboardingStatusService;
    private readonly OnboardingSetupService _onboardingSetupService;
    private readonly BalancePlatformService _balancePlatformService;
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
    private readonly IAdminTenantService _adminTenantService;

    public OnboardingService(
        OnboardingStatusService onboardingStatusService,
        OnboardingSetupService onboardingSetupService,
        BalancePlatformService balancePlatformService,
        ITenantRepository tenantRepository,
        ISubscriptionPlanRepository subscriptionPlanRepository,
        IAdminTenantService adminTenantService)
    {
        _onboardingStatusService = onboardingStatusService;
        _onboardingSetupService = onboardingSetupService;
        _balancePlatformService = balancePlatformService;
        _tenantRepository = tenantRepository;
        _subscriptionPlanRepository = subscriptionPlanRepository;
        _adminTenantService = adminTenantService;
    }

    public Task<SetupBusinessResultDto> SetupBusinessAsync(
        string userId,
        string businessName,
        Domain.Enums.BusinessType businessType,
        string countryCode,
        string city,
        string currencyCode,
        string? logoUrl,
        string? predeterminedTenantId = null,
        CancellationToken cancellationToken = default)
        => _onboardingSetupService.SetupBusinessAsync(
            userId,
            businessName,
            businessType,
            countryCode,
            city,
            currencyCode,
            logoUrl,
            predeterminedTenantId,
            cancellationToken);

    public Task<OnboardingResultDto> CompleteOnboardingAsync(
        string userId,
        string planId,
        Domain.Enums.BillingType billingType,
        CancellationToken cancellationToken = default)
        => _onboardingSetupService.CompleteOnboardingAsync(
            userId,
            planId,
            billingType,
            cancellationToken);

    public Task<OnboardingStatusDto> GetOnboardingStatusAsync(
        string userId,
        CancellationToken cancellationToken = default)
        => _onboardingStatusService.GetOnboardingStatusAsync(userId, cancellationToken);

    public Task<AddBalancePlatformResultDto> AddBalancePlatformAsync(
        string userId,
        decimal amount,
        string screenShotUrl,
        CancellationToken cancellationToken = default)
        => _balancePlatformService.AddBalancePlatformAsync(
            userId,
            amount,
            screenShotUrl,
            cancellationToken);

    public async Task ActivateSubscriptionAsync(
        string userId,
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByOwnerUserIdForStatusAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), userId);

        var ownsSubscription = tenant.Subscriptions.Any(s => s.Id == subscriptionId);
        if (!ownsSubscription)
        {
            throw new NotFoundException(nameof(TenantSubscription), subscriptionId);
        }

        await _adminTenantService.ActivateSubscriptionAsync(subscriptionId, cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> GetSubscriptionPlansAsync(
        CancellationToken cancellationToken = default)
    {
        var plans = await _subscriptionPlanRepository.GetAllActiveAsync(cancellationToken);

        return plans.Select(plan => new SubscriptionPlanDto(
            plan.Id,
            plan.Name,
            plan.Code,
            plan.MonthlyPrice,
            plan.YearlyPrice,
            plan.TrialDays,
            ParseFeatures(plan.FeaturesJson),
            plan.IsActive)).ToList();
    }

    private static IReadOnlyList<string> ParseFeatures(string featuresJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(featuresJson) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
