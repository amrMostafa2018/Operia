using Operia.Domain.Common;

namespace Operia.Domain.Entities;

public sealed class EmployeeWorkingDay : Entity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string Day { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }

    public Employee? Employee { get; set; }
    public Tenant? Tenant { get; set; }
}
