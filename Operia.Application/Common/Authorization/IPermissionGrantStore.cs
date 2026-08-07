namespace Operia.Application.Common.Authorization;

public interface IPermissionGrantStore
{
    Task<bool> HasPermissionAsync(
        string userId,
        string permission,
        CancellationToken cancellationToken = default);
}
