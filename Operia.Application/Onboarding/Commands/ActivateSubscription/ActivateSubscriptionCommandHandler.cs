using MediatR;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Onboarding.Commands.ActivateSubscription;

public sealed class ActivateSubscriptionCommandHandler : IRequestHandler<ActivateSubscriptionCommand>
{
    private readonly IOnboardingService _onboardingService;
    private readonly ICurrentUserService _currentUserService;

    public ActivateSubscriptionCommandHandler(
        IOnboardingService onboardingService,
        ICurrentUserService currentUserService)
    {
        _onboardingService = onboardingService;
        _currentUserService = currentUserService;
    }

    public Task Handle(ActivateSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException();

        return _onboardingService.ActivateSubscriptionAsync(
            userId,
            request.SubscriptionId,
            cancellationToken);
    }
}
