using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.DTOs;

namespace Operia.Application.Onboarding.Queries.GetOnboardingStatus;
public sealed class GetOnboardingStatusQueryHandler
    : IRequestHandler<GetOnboardingStatusQuery, OnboardingStatusDto>
{
    private readonly IOnboardingService _onboardingService;
    private readonly ICurrentUserService _currentUserService;
    public GetOnboardingStatusQueryHandler(
        IOnboardingService onboardingService,
        ICurrentUserService currentUserService)
    {
        _onboardingService = onboardingService;
        _currentUserService = currentUserService;
    }
    public Task<OnboardingStatusDto> Handle(
        GetOnboardingStatusQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException();

        return _onboardingService.GetOnboardingStatusAsync(userId, cancellationToken);
    }
}

