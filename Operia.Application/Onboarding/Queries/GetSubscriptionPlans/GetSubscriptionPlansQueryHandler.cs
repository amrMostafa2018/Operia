using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Onboarding.DTOs;

namespace Operia.Application.Onboarding.Queries.GetSubscriptionPlans;

public sealed class GetSubscriptionPlansQueryHandler
    : IRequestHandler<GetSubscriptionPlansQuery, IReadOnlyList<SubscriptionPlanDto>>
{
    private readonly IOnboardingService _onboardingService;

    public GetSubscriptionPlansQueryHandler(IOnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    public Task<IReadOnlyList<SubscriptionPlanDto>> Handle(
        GetSubscriptionPlansQuery request,
        CancellationToken cancellationToken)
        => _onboardingService.GetSubscriptionPlansAsync(cancellationToken);
}
