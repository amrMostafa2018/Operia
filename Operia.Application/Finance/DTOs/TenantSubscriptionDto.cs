namespace Operia.Application.Finance.DTOs;

public sealed record TenantSubscriptionDto(
    string Id,
    string PlanCode,
    string PlanName,
    string BillingType,
    decimal Amount,
    string Currency,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status);
