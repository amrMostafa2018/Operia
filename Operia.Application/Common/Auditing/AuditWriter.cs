using System.Text.Json;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;

namespace Operia.Application.Common.Auditing;

public sealed class AuditWriter(
    IApplicationDbContext db,
    ICurrentUserService currentUser) : IAuditWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Write(
        string tenantId,
        string action,
        string entityType,
        string entityId,
        string entityName,
        object? details = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = currentUser.UserId,
            UserDisplayName = currentUser.DisplayName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityName = entityName,
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details, JsonOptions)
        });
    }
}
