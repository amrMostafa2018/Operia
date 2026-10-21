using Operia.Domain.Common;
using Operia.Domain.Enums;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

/// <summary>Stores one priced service or Package line as it appeared when the booking was saved.</summary>
public sealed class BookingItem : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string BookingId { get; set; } = string.Empty;
    public BookingItemType Type { get; set; }
    public string? PackageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    /// <summary>Saved duration for one unit, independent of later catalog edits.</summary>
    public int DurationMinutes { get; set; }
    /// <summary>Saved price per unit; zero for a reserved Package or reused service session.</summary>
    public decimal UnitPrice { get; set; }

    public Booking? Booking { get; set; }
    public Package? Package { get; set; }
}
