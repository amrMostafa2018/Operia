namespace Operia.Application.Common.Authorization;

public interface IPermissionGrantStore
{
    Task<PermissionGrantSnapshot?> GetUserCapabilitiesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> HasPermissionAsync(
        string userId,
        string permission,
        CancellationToken cancellationToken = default);
}

public sealed record PermissionGrantSnapshot(
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
