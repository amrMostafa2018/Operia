using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Interfaces;
using Operia.SharedKernel.Interfaces;
namespace Operia.Infrastructure.Services;

public sealed class OnboardingService : IOnboardingService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IBusinessRepository _businessRepository;
    private readonly IBusinessGalleryRepository _businessGalleryRepository;
    private readonly IPlatformService _platformService;
    private readonly IIdentityService _identityService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public OnboardingService(
        ITenantRepository tenantRepository,
        IBusinessRepository businessRepository,
        IBusinessGalleryRepository businessGalleryRepository,
        IPlatformService platformService,
        IIdentityService identityService,
        IFileStorageService fileStorageService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _businessRepository = businessRepository;
        _businessGalleryRepository = businessGalleryRepository;
        _platformService = platformService;
        _identityService = identityService;
        _fileStorageService = fileStorageService;
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
        string? predeterminedTenantId = null,
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
            Id = predeterminedTenantId ?? Guid.NewGuid().ToString(),
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

        var pendingAddBalancePlatform = await _platformService.GetPendingAddBalancePlatformAsync(
            tenant.Id,
            cancellationToken);
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

    public async Task<TenantUploadContextDto> ResolveUploadTenantContextAsync(
        string userId,
        string? currentTenantId,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(currentTenantId))
            return new TenantUploadContextDto(currentTenantId, false);

        var existingTenant = await _tenantRepository.GetByOwnerUserIdWithDetailsAsync(
            userId,
            cancellationToken);

        if (existingTenant is not null)
            return new TenantUploadContextDto(existingTenant.Id, false);

        return new TenantUploadContextDto(Guid.NewGuid().ToString(), true);
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
            if (existingLogo.ImageUrl.StartsWith("/uploads/", StringComparison.Ordinal))
                await _fileStorageService.DeleteAsync(existingLogo.ImageUrl, cancellationToken);

            existingLogo.ImageUrl = logoUrl;
            existingLogo.UploadedAt = _dateTimeProvider.UtcNow;
        }
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
