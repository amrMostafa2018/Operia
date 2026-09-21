using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

/// <summary>Represents a temporary interval hold that participates in availability checks.</summary>
public sealed class BookingHold : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public DateOnly ScheduledDate { get; set; }
    public int StartMinutes { get; set; }
    public int EndMinutes { get; set; }
    /// <summary>UTC expiry after which the interval no longer blocks availability.</summary>
    public DateTime ExpiresAtUtc { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    /// <summary>Set when an eligible hold has become a confirmed booking.</summary>
    public string? ConvertedBookingId { get; set; }

    public Branch? Branch { get; set; }
    public Employee? Employee { get; set; }
    public Booking? ConvertedBooking { get; set; }
}
