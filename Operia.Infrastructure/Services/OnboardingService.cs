using System.Text.Json;
using FluentValidation.Results;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Exceptions;
using Operia.Domain.Interfaces;
using Operia.SharedKernel.Interfaces;

namespace Operia.Infrastructure.Services;

public sealed class OnboardingService : IOnboardingService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IBusinessRepository _businessRepository;
    private readonly IBusinessGalleryRepository _businessGalleryRepository;
    private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
    private readonly IIdentityService _identityService;
    private readonly IAdminTenantService _adminTenantService;
    private readonly IPlatformRevenueRepository _platformRevenueRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public OnboardingService(
        ITenantRepository tenantRepository,
        IBusinessRepository businessRepository,
        IBusinessGalleryRepository businessGalleryRepository,
        ISubscriptionPlanRepository subscriptionPlanRepository,
        ITenantSubscriptionRepository tenantSubscriptionRepository,
        IIdentityService identityService,
        IAdminTenantService adminTenantService,
        IPlatformRevenueRepository platformRevenueRepository,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _businessRepository = businessRepository;
        _businessGalleryRepository = businessGalleryRepository;
        _subscriptionPlanRepository = subscriptionPlanRepository;
        _tenantSubscriptionRepository = tenantSubscriptionRepository;
        _identityService = identityService;
        _adminTenantService = adminTenantService;
        _platformRevenueRepository = platformRevenueRepository;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<SetupBusinessResultDto> SetupBusinessAsync(
        string userId,
        string businessName,
        BusinessType businessType,
        string countryCode,
        string city,
        string currencyCode,
        string? logoUrl,
        CancellationToken cancellationToken = default)
    {
        var existingTenant = await _tenantRepository.GetByOwnerUserIdWithDetailsAsync(userId, cancellationToken);

        if (existingTenant is not null)
        {
            existingTenant.BusinessName = businessName.Trim();
            existingTenant.BusinessType = businessType;
            existingTenant.CountryCode = countryCode.ToUpperInvariant();
            existingTenant.City = city;
            existingTenant.CurrencyCode = currencyCode.ToUpperInvariant();

            var business = existingTenant.Businesses.FirstOrDefault();
            if (business is null)
            {
                business = CreateBusiness(existingTenant.Id, businessName);
                await _businessRepository.AddAsync(business, cancellationToken);
            }
            else
            {
                business.ActivityName = businessName.Trim();
            }

            await UpdateLogoAsync(business, logoUrl, cancellationToken);
            await _identityService.SetUserTenantIdAsync(userId, existingTenant.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SetupBusinessResultDto(existingTenant.Id, business.Id);
        }

        var tenant = new Tenant
        {
            OwnerUserId = userId,
            BusinessName = businessName.Trim(),
            BusinessType = businessType,
            CountryCode = countryCode.ToUpperInvariant(),
            City = city,
            CurrencyCode = currencyCode.ToUpperInvariant(),
            Timezone = ResolveTimezone(countryCode),
            Status = TenantStatus.Active,
            Balance = 0
        };

        var newBusiness = CreateBusiness(tenant.Id, businessName);

        await _tenantRepository.AddAsync(tenant, cancellationToken);
        await _businessRepository.AddAsync(newBusiness, cancellationToken);
        await UpdateLogoAsync(newBusiness, logoUrl, cancellationToken);
        await _identityService.SetUserTenantIdAsync(userId, tenant.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SetupBusinessResultDto(tenant.Id, newBusiness.Id);
    }

    public async Task<OnboardingResultDto> CompleteOnboardingAsync(
        string userId,
        string planId,
        BillingType billingType,
        string screenShotUrl,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByOwnerUserIdWithDetailsAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), userId);

        var business = tenant.Businesses.FirstOrDefault()
            ?? throw new NotFoundException(nameof(Business), tenant.Id);

        var pendingSubscription = tenant.Subscriptions
            .FirstOrDefault(s => s.Status == SubscriptionStatus.Pending);

        if (pendingSubscription is not null)
        {
            return new OnboardingResultDto(
                tenant.Id,
                business.Id,
                pendingSubscription.Id,
                pendingSubscription.Status.ToString());
        }

        var plan = await _subscriptionPlanRepository.GetActiveByIdAsync(planId, cancellationToken)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), planId);

        var amount = billingType == BillingType.Monthly
            ? plan.MonthlyPrice
            : plan.YearlyPrice;

        var subscription = new TenantSubscription
        {
            TenantId = tenant.Id,
            PlanId = plan.Id,
            Amount = amount,
            Currency = tenant.CurrencyCode,
            BillingType = billingType,
            ScreenShotUrl = screenShotUrl,
            Status = SubscriptionStatus.Pending
        };

        await _tenantSubscriptionRepository.AddAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new OnboardingResultDto(
            tenant.Id,
            business.Id,
            subscription.Id,
            subscription.Status.ToString());
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

    public async Task<AddBalancePlatformResultDto> AddBalancePlatformAsync(
        string userId,
        decimal amount,
        string screenShotUrl,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByOwnerUserIdForStatusAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), userId);

        var existingPending = await _platformRevenueRepository.GetLatestPendingAddBalancePlatformByTenantIdAsync(
            tenant.Id,
            cancellationToken);

        if (existingPending is not null)
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    "addBalancePlatform",
                    "A balance add request is already pending review.")
            ]);
        }

        if (string.IsNullOrWhiteSpace(screenShotUrl))
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    "screenShotUrl",
                    "Balance add request must include an Instapay screenshot.")
            ]);
        }

        var revenue = new PlatformRevenue
        {
            TenantId = tenant.Id,
            Amount = amount,
            Currency = tenant.CurrencyCode,
            ScreenShotUrl = screenShotUrl,
            Status = PlatformRevenueStatus.Pending,
            RecordedAt = _dateTimeProvider.UtcNow
        };

        await _platformRevenueRepository.AddAsync(revenue, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddBalancePlatformResultDto(revenue.Id, revenue.Amount);
    }

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

    private static Business CreateBusiness(string tenantId, string businessName) =>
        new()
        {
            TenantId = tenantId,
            ActivityName = businessName.Trim(),
            Status = BusinessStatus.Active
        };

    private async Task UpdateLogoAsync(
        Business business,
        string? logoUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return;

        var existingLogo = await _businessGalleryRepository.GetMainImageByBusinessIdAsync(
            business.Id,
            cancellationToken);

        if (existingLogo is null)
        {
            await _businessGalleryRepository.AddAsync(new BusinessGallery
            {
                BusinessId = business.Id,
                ImageUrl = logoUrl,
                IsMainImage = true,
                UploadedAt = _dateTimeProvider.UtcNow
            }, cancellationToken);
        }
        else
        {
            existingLogo.ImageUrl = logoUrl;
            existingLogo.UploadedAt = _dateTimeProvider.UtcNow;
        }
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

    private static string ResolveTimezone(string countryCode) =>
        countryCode.ToUpperInvariant() switch
        {
            "EG" => "Africa/Cairo",
            "SA" => "Asia/Riyadh",
            "AE" => "Asia/Dubai",
            "KW" => "Asia/Kuwait",
            "QA" => "Asia/Qatar",
            "BH" => "Asia/Bahrain",
            "OM" => "Asia/Muscat",
            "JO" => "Asia/Amman",
            _ => "UTC"
        };
}
