using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Exceptions;
using Operia.Domain.Interfaces;
using Operia.SharedKernel.Interfaces;

namespace Operia.Infrastructure.Services;

public sealed class OnboardingSetupService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IBusinessRepository _businessRepository;
    private readonly IBusinessGalleryRepository _businessGalleryRepository;
    private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
    private readonly IIdentityService _identityService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public OnboardingSetupService(
        ITenantRepository tenantRepository,
        IBusinessRepository businessRepository,
        IBusinessGalleryRepository businessGalleryRepository,
        ISubscriptionPlanRepository subscriptionPlanRepository,
        ITenantSubscriptionRepository tenantSubscriptionRepository,
        IIdentityService identityService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _businessRepository = businessRepository;
        _businessGalleryRepository = businessGalleryRepository;
        _subscriptionPlanRepository = subscriptionPlanRepository;
        _tenantSubscriptionRepository = tenantSubscriptionRepository;
        _identityService = identityService;
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
