namespace Operia.Domain.Interfaces;

/// <summary>
/// Marks a record whose access and persistence are scoped to one tenant.
/// </summary>
public interface ITenantScoped
{
    string TenantId { get; }
}
