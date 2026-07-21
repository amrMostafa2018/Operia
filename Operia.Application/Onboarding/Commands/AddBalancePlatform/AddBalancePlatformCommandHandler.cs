using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.DTOs;

namespace Operia.Application.Onboarding.Commands.AddBalancePlatform;

public sealed class AddBalancePlatformCommandHandler
    : IRequestHandler<AddBalancePlatformCommand, AddBalancePlatformResultDto>
{
    private readonly IOnboardingService _onboardingService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;

    public AddBalancePlatformCommandHandler(
        IOnboardingService onboardingService,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService)
    {
        _onboardingService = onboardingService;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
    }

    public async Task<AddBalancePlatformResultDto> Handle(
        AddBalancePlatformCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException();

        await using var screenshot = request.Screenshot!;

        var uploadContext = await _onboardingService.ResolveUploadTenantContextAsync(
            userId,
            _currentUserService.TenantId,
            cancellationToken);

        var screenShotUrl = await _fileStorageService.SaveAsync(
            screenshot.Content,
            screenshot.FileName,
            screenshot.ContentType,
            uploadContext.TenantId,
            FileUploadCategory.PlatformRevenue,
            cancellationToken);

        return await _onboardingService.AddBalancePlatformAsync(
            userId,
            request.Amount,
            screenShotUrl,
            cancellationToken);
    }
}
