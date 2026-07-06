namespace Operia.Domain.Entities;

public sealed class PlatformRevenue
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string? SubscriptionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public TenantSubscription? Subscription { get; set; }
}
