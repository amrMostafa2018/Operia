using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

/// <summary>A confirmed sale awaiting collection or an appointment.</summary>
public sealed class Sale : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public decimal DueNow { get; set; }
    public string ItemsSnapshotJson { get; set; } = "[]";
    public string Disposition { get; set; } = "Later";
    public string Status { get; set; } = "PendingCollection";
}
