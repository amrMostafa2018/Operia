namespace Operia.Application.Common.Auditing;

public interface IAuditWriter
{
    void Write(
        string tenantId,
        string action,
        string entityType,
        string entityId,
        string entityName,
        object? details = null);
}
