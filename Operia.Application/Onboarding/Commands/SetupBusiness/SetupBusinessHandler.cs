using MediatR;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.Common;
using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Interfaces;
using Operia.SharedKernel.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Onboarding.Commands.SetupBusiness;

public sealed class SetupBusinessHandler
    : IRequestHandler<SetupBusinessCommand, SetupBusinessResultDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IApplicationDbContext _dbContext;
    private readonly IOnboardingBusinessGalleryStore _businessGalleryStore;
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public SetupBusinessHandler(
        ITenantRepository tenantRepository,
        IApplicationDbContext dbContext,
        IOnboardingBusinessGalleryStore businessGalleryStore,
        IIdentityService identityService,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _dbContext = dbContext;
        _businessGalleryStore = businessGalleryStore;
        _identityService = identityService;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<SetupBusinessResultDto> Handle(
        SetupBusinessCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");

        string? logoUrl = null;
        string? predeterminedTenantId = null;

        if (request.Logo is not null)
        {
            await using (request.Logo)
            {
                var uploadContext = await OnboardingHandlerHelpers.ResolveUploadTenantContextAsync(
                    _tenantRepository,
                    userId,
                    _currentUserService.TenantId,
                    cancellationToken);

                if (uploadContext.IsNewTenant)
                    predeterminedTenantId = uploadContext.TenantId;

                logoUrl = await _fileStorageService.SaveAsync(
                    request.Logo.Content,
                    request.Logo.FileName,
                    request.Logo.ContentType,
                    uploadContext.TenantId,
                    FileUploadCategory.BusinessGallery,
                    cancellationToken);
            }
        }

        var existingTenant = await _tenantRepository.GetByOwnerUserIdWithDetailsAsync(userId, cancellationToken);

        if (existingTenant is not null)
        {
            existingTenant.BusinessName = request.BusinessName.Trim();
            existingTenant.BusinessType = request.BusinessType;
            existingTenant.CountryCode = request.CountryCode.ToUpperInvariant();
            existingTenant.City = request.City;
            existingTenant.CurrencyCode = request.CurrencyCode.ToUpperInvariant();

            var business = existingTenant.Businesses.FirstOrDefault();
            if (business is null)
            {
                business = CreateBusiness(existingTenant.Id, request.BusinessName);
                await _dbContext.Businesses.AddAsync(business, cancellationToken);
            }
            else
            {
                business.ActivityName = request.BusinessName.Trim();
            }

            await UpdateLogoAsync(business, logoUrl, cancellationToken);
            await PersistSetupAsync(userId, existingTenant.Id, cancellationToken);

            return new SetupBusinessResultDto(existingTenant.Id, business.Id);
        }

        var tenant = new Tenant
        {
            Id = predeterminedTenantId ?? Guid.NewGuid().ToString(),
            OwnerUserId = userId,
            BusinessName = request.BusinessName.Trim(),
            BusinessType = request.BusinessType,
            CountryCode = request.CountryCode.ToUpperInvariant(),
            City = request.City,
            CurrencyCode = request.CurrencyCode.ToUpperInvariant(),
            Timezone = ResolveTimezone(request.CountryCode),
            Status = TenantStatus.Active,
            Balance = 0
        };

        var newBusiness = CreateBusiness(tenant.Id, request.BusinessName);

        await _tenantRepository.AddAsync(tenant, cancellationToken);
        await _dbContext.Businesses.AddAsync(newBusiness, cancellationToken);
        await UpdateLogoAsync(newBusiness, logoUrl, cancellationToken);
        await PersistSetupAsync(userId, tenant.Id, cancellationToken);

        return new SetupBusinessResultDto(tenant.Id, newBusiness.Id);
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

        var existingLogo = await _businessGalleryStore.GetMainImageForOwnerScopeAsync(
            business.Id,
            business.TenantId,
            cancellationToken);

        if (existingLogo is null)
        {
            await _dbContext.BusinessGalleries.AddAsync(new BusinessGallery
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

    private async Task PersistSetupAsync(
        string userId,
        string tenantId,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _identityService.SetUserTenantIdAsync(userId, tenantId, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
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
