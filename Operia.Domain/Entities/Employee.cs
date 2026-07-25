using Operia.Domain.Common;

namespace Operia.Domain.Entities;

public sealed class Employee : Entity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string IdentityUserId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public string? JobTitle { get; set; }
    public DateOnly JoiningDate { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public Tenant? Tenant { get; set; }
    public ICollection<UserBranch> UserBranches { get; set; } = [];
}


public sealed class TenantNumberCounter : Entity
{
    public string TenantId { get; set; } = string.Empty;
    public int LastEmployeeNumber { get; set; }
    public Tenant? Tenant { get; set; }
}
