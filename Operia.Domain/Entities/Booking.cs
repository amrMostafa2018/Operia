using Operia.Domain.Common;
using Operia.Domain.Enums;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

/// <summary>Stores the tenant-scoped appointment, its scheduled interval, status, pricing snapshot, and concurrency version.</summary>
public sealed class Booking : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string BookingNumber { get; set; } = string.Empty;
    /// <summary>Identifies a create request so a retry returns the same booking.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerMobile { get; set; } = string.Empty;
    /// <summary>The scheduled branch-local calendar date; no time zone is stored on the branch.</summary>
    public DateOnly ScheduledDate { get; set; }
    /// <summary>Start of the reserved interval, in minutes after local midnight.</summary>
    public int StartMinutes { get; set; }
    /// <summary>End of the reserved interval, in minutes after local midnight.</summary>
    public int EndMinutes { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Booked;
    public string Source { get; set; } = "AdminPortal";
    /// <summary>The selected method identifier; payment settlement is tracked separately.</summary>
    public string? PaymentMethod { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    /// <summary>Database row version used to reject edits to a changed booking.</summary>
    public byte[] Version { get; set; } = [];

    public Branch? Branch { get; set; }
    public Employee? Employee { get; set; }
    public ICollection<BookingItem> Items { get; set; } = [];
    public ICollection<BookingHistory> History { get; set; } = [];
}
