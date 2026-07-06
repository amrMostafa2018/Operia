namespace Operia.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<(bool Succeeded, string UserId, IEnumerable<string> Errors)> ValidateCredentialsAsync(
        string phoneNumber,
        string password,
        CancellationToken cancellationToken = default);

    Task<bool> IsPhoneRegisteredAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(string userId, CancellationToken cancellationToken = default);

    Task<string?> GetUserIdByPhoneAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);

    Task<string> GeneratePasswordResetTokenAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(
        string userId,
        string resetToken,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task SetUserTenantIdAsync(
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default);
}
