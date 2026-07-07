using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.DTOs;

namespace Operia.Application.Onboarding.Commands.AddBalancePlatform;

public sealed class AddBalancePlatformCommandHandler
    : IRequestHandler<AddBalancePlatformCommand, AddBalancePlatformResultDto>
{
    private readonly IOnboardingService _onboardingService;
    private readonly ICurrentUserService _currentUserService;

    public AddBalancePlatformCommandHandler(
        IOnboardingService onboardingService,
        ICurrentUserService currentUserService)
    {
        _onboardingService = onboardingService;
        _currentUserService = currentUserService;
    }

    public Task<AddBalancePlatformResultDto> Handle(
        AddBalancePlatformCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException();

        return _onboardingService.AddBalancePlatformAsync(
            userId,
            request.Amount,
            request.ScreenShotUrl,
            cancellationToken);
    }
}
