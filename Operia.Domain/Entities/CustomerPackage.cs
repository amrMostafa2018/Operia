using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

/// <summary>Tracks a customer's purchased sessions or pulses and their used and reserved balances.</summary>
public sealed class CustomerPackage : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;

    /// <summary>Total sessions or pulses purchased for this package or standalone service unit.</summary>
    public int Total { get; set; }

    /// <summary>Sessions or pulses consumed by completed bookings.</summary>
    public int Used { get; set; }

    /// <summary>
    /// Session slots held by active booking reservations but not yet consumed.
    /// Stored for all package types; pulse remaining balance ignores this value when calculating availability.
    /// </summary>
    public int? ReservedSessions { get; set; }

    public DateOnly? ExpiresOn { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Database row version used when a balance changes concurrently.</summary>
    public byte[] Version { get; set; } = [];

    public Customer? Customer { get; set; }
    public Package? Package { get; set; }
    public ICollection<BookingPackageReservation> Reservations { get; set; } = [];

    /// <summary>Reserved session count; returns 0 when <see cref="ReservedSessions"/> is null.</summary>
    public int ReservedSessionCount => ReservedSessions ?? 0;

    /// <summary>Remaining units using session-package rules only. Use application balance helpers for pulse packages.</summary>
    public int RemainingBalance => Total - Used - ReservedSessionCount;

    /// <summary>Whether the purchase still has available balance.</summary>
    public bool HasAvailableBalance => RemainingBalance > 0;
}
