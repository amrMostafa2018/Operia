namespace Operia.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }

    bool IsInRole(string role);
}
