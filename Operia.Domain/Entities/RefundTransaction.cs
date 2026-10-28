using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

/// <summary>A posted refund movement against one saved customer purchase.</summary>
public sealed class RefundTransaction : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string CustomerPackageId { get; set; } = string.Empty;
    public string SaleId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
}
