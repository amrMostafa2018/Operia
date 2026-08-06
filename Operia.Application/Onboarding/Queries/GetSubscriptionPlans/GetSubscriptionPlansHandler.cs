using System.Text.Json;
using MediatR;
using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Interfaces;

namespace Operia.Application.Onboarding.Queries.GetSubscriptionPlans;

public sealed class GetSubscriptionPlansHandler
    : IRequestHandler<GetSubscriptionPlansQuery, IReadOnlyList<SubscriptionPlanDto>>
{
    private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;

    public GetSubscriptionPlansHandler(ISubscriptionPlanRepository subscriptionPlanRepository)
    {
        _subscriptionPlanRepository = subscriptionPlanRepository;
    }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> Handle(
        GetSubscriptionPlansQuery request,
        CancellationToken cancellationToken)
    {
        var plans = await _subscriptionPlanRepository.GetAllActiveAsync(cancellationToken);

        return plans.Select(plan => new SubscriptionPlanDto(
            plan.Id,
            plan.Name,
            plan.Code,
            plan.MonthlyPrice,
            plan.YearlyPrice,
            plan.TrialDays,
            ParseFeatures(plan.FeaturesJson),
            plan.IsActive)).ToList();
    }

    private static IReadOnlyList<string> ParseFeatures(string featuresJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(featuresJson) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
