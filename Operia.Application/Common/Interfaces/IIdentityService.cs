namespace Operia.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<(bool Succeeded, string UserId, IEnumerable<string> Errors)> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(string userId, CancellationToken cancellationToken = default);
}
