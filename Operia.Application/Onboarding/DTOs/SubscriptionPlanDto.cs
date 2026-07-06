namespace Operia.Application.Onboarding.DTOs;

public sealed record SubscriptionPlanDto(
    string PlanId,
    string Name,
    string Code,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    int TrialDays,
    IReadOnlyList<string> Features,
    bool IsActive);
