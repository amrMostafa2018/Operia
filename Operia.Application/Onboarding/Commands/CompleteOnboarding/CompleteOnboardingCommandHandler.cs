using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.DTOs;
namespace Operia.Application.Onboarding.Commands.CompleteOnboarding;
public sealed class CompleteOnboardingCommandHandler
    : IRequestHandler<CompleteOnboardingCommand, OnboardingResultDto>
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ICurrentUserService _currentUserService;
    public CompleteOnboardingCommandHandler(
        ISubscriptionService subscriptionService,
        ICurrentUserService currentUserService)
    {
        _subscriptionService = subscriptionService;
        _currentUserService = currentUserService;
    }
    public Task<OnboardingResultDto> Handle(
        CompleteOnboardingCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException();
        return _subscriptionService.SelectPlanAsync(
            userId,
            request.PlanId,
            request.BillingType,
            cancellationToken);
    }
}
