using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

/// <summary>Tracks a customer's purchased sessions and their used and reserved balances.</summary>
public sealed class CustomerPackage : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    /// <summary>Sessions purchased for this Package or one-session standalone service unit.</summary>
    public int TotalSessions { get; set; }
    /// <summary>Sessions consumed by a completed booking.</summary>
    public int UsedSessions { get; set; }
    /// <summary>Sessions held by active booking reservations but not yet used.</summary>
    public int ReservedSessions { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Database row version used when a balance changes concurrently.</summary>
    public byte[] Version { get; set; } = [];

    public Customer? Customer { get; set; }
    public Package? Package { get; set; }
    public ICollection<BookingPackageReservation> Reservations { get; set; } = [];
}
