using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

public sealed class PackageStaff : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;

    public Package? Package { get; set; }
    public Employee? Employee { get; set; }
}
