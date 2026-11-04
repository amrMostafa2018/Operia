using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

/// <summary>A confirmed cashier collection against one numbered row of a saved purchase plan.</summary>
public sealed class InstallmentCollection : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string CustomerPackageId { get; set; } = string.Empty;
    public int InstallmentNumber { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CollectedByUserId { get; set; } = string.Empty;
    public DateTime ConfirmedAtUtc { get; set; }
    public CustomerPackage? CustomerPackage { get; set; }
}
