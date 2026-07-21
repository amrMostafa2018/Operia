using Operia.Domain.Common;

namespace Operia.Domain.Entities;

public sealed class Branch : Entity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string GoogleMapsUrl { get; set; } = string.Empty;
    public Tenant? Tenant { get; set; }
}
