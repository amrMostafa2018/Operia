using Operia.Domain.Common;
using Operia.Domain.Enums;

namespace Operia.Domain.Entities;

public sealed class TenantSubscription : Entity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public BillingType BillingType { get; set; }
    public string? ScreenShotUrl { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;
    public Tenant? Tenant { get; set; }
    public SubscriptionPlan? Plan { get; set; }
}
