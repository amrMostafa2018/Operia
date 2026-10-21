using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

/// <summary>Records an actor-visible change to a booking without replacing earlier history.</summary>
public sealed class BookingHistory : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string BookingId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? ChangedByUserId { get; set; }
    public string? ChangedByDisplayName { get; set; }
    public string? ChangesJson { get; set; }

    public Booking? Booking { get; set; }
}
