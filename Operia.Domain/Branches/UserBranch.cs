using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

public sealed class UserBranch : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public Employee? Employee { get; set; }
    public Branch? Branch { get; set; }
}
