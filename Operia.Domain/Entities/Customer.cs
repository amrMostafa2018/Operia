using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

/// <summary>Represents a registered tenant customer available for appointment lookup.</summary>
public sealed class Customer : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string NormalizedMobileNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<CustomerPackage> Packages { get; set; } = [];
}
