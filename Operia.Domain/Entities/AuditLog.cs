using Operia.Domain.Common;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

public sealed class AuditLog : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? DetailsJson { get; set; }
}
