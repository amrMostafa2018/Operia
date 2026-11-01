using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

/// <summary>A purchase-time snapshot of one installment and its confirmed collection.</summary>
public sealed class CustomerPackageInstallment : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string CustomerPackageId { get; set; } = string.Empty;
    /// <summary>Zero is the first payment; positive numbers follow the catalog schedule.</summary>
    public int Number { get; set; }
    public decimal Amount { get; set; }
    public int CumulativeAvailableSessions { get; set; }
    public DateTime? CollectedAtUtc { get; set; }
    public string? PaymentMethod { get; set; }
    public byte[] Version { get; set; } = [];
    public CustomerPackage? CustomerPackage { get; set; }
}
