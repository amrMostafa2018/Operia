using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.DTOs;

namespace Operia.Application.Onboarding.Commands.SetupBusiness;

public sealed class SetupBusinessCommandHandler
    : IRequestHandler<SetupBusinessCommand, SetupBusinessResultDto>
{
    private readonly IOnboardingService _onboardingService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;

    public SetupBusinessCommandHandler(
        IOnboardingService onboardingService,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService)
    {
        _onboardingService = onboardingService;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
    }

    public async Task<SetupBusinessResultDto> Handle(
        SetupBusinessCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException();

        string? logoUrl = null;
        string? predeterminedTenantId = null;

        if (request.Logo is not null)
        {
            await using (request.Logo)
            {
                var uploadContext = await _onboardingService.ResolveUploadTenantContextAsync(
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

        return await _onboardingService.SetupBusinessAsync(
            userId,
            request.BusinessName,
            request.BusinessType,
            request.CountryCode,
            request.City,
            request.CurrencyCode,
            logoUrl,
            predeterminedTenantId,
            cancellationToken);
    }
}
