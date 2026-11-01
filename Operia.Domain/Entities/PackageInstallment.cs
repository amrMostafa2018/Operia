using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

public sealed class PackageInstallment : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public int Number { get; set; }
    public decimal Amount { get; set; }
    public int SessionsUnlocked { get; set; }
    public int CumulativeAvailableSessions { get; set; }
    public Package? Package { get; set; }
}
