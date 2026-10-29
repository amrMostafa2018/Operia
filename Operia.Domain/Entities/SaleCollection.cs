using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

/// <summary>A traceable cashier collection attempt against one immutable sale.</summary>
public sealed class SaleCollection : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string SaleId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public bool Confirmed { get; set; }
    /// <summary>Null on older cashier attempts; their Confirmed flag supplies the legacy state.</summary>
    public string? ReviewStatus { get; set; }
    public byte[]? ProofContent { get; set; }
    public string? ProofContentType { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime AttemptedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public string CollectedByUserId { get; set; } = string.Empty;
}
