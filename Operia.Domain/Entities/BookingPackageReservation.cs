using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

/// <summary>Links a booked session to the customer purchase whose balance it reserves.</summary>
public sealed class BookingPackageReservation : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string BookingId { get; set; } = string.Empty;
    public string CustomerPackageId { get; set; } = string.Empty;
    /// <summary>The numbered session reserved from the linked customer purchase.</summary>
    public int SessionNumber { get; set; }
    /// <summary>UTC release time; a null value means this reservation remains active.</summary>
    public DateTime? ReleasedAtUtc { get; set; }

    public Booking? Booking { get; set; }
    public CustomerPackage? CustomerPackage { get; set; }
}
