using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.DTOs;

namespace Operia.Application.Onboarding.Commands.CompleteOnboarding;

public sealed class CompleteOnboardingCommandHandler
    : IRequestHandler<CompleteOnboardingCommand, OnboardingResultDto>
{
    private readonly IOnboardingService _onboardingService;
    private readonly ICurrentUserService _currentUserService;

    public CompleteOnboardingCommandHandler(
        IOnboardingService onboardingService,
        ICurrentUserService currentUserService)
    {
        _onboardingService = onboardingService;
        _currentUserService = currentUserService;
    }

    public Task<OnboardingResultDto> Handle(
        CompleteOnboardingCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException();

        return _onboardingService.CompleteOnboardingAsync(
            userId,
            request.PlanId,
            request.BillingType,
            request.ScreenShotUrl,
            cancellationToken);
    }
}
