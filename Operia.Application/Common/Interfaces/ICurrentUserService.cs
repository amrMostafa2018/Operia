namespace Operia.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }

    string? TenantId { get; }
    string? DisplayName { get; }

    bool IsInRole(string role);
}
