using Operia.Domain.Common;
using Operia.Domain.Enums;

namespace Operia.Domain.Entities;

public sealed class Tenant : Entity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OwnerUserId { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public BusinessType BusinessType { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string Timezone { get; set; } = "Africa/Cairo";
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public decimal Balance { get; set; }

    public ICollection<Business> Businesses { get; set; } = [];
    public ICollection<TenantSubscription> Subscriptions { get; set; } = [];
    public ICollection<PlatformRevenue> PlatformRevenues { get; set; } = [];
}
