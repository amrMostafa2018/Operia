using Operia.Domain.Common;
using Operia.Domain.Enums;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

public sealed class TenantSubscription : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public BillingType BillingType { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;
    public Tenant? Tenant { get; set; }
    public SubscriptionPlan? Plan { get; set; }
}
