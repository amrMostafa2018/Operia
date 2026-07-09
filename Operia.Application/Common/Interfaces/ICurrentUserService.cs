namespace Operia.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }

    string? TenantId { get; }

    bool IsInRole(string role);
}
