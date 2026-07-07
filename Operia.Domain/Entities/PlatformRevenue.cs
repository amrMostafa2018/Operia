using Operia.Domain.Enums;

namespace Operia.Domain.Entities;

public sealed class PlatformRevenue
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string ScreenShotUrl { get; set; } = string.Empty;
    public PlatformRevenueStatus Status { get; set; } = PlatformRevenueStatus.Pending;
    public DateTime RecordedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public Tenant? Tenant { get; set; }
}
