namespace Operia.Application.Common.Interfaces;

public record UserSummaryDto(
    string Id,
    string UserName,
    string Email,
    string PhoneNumber,
    bool IsLockedOut,
    string FullName = "");

public interface IIdentityService
{
    Task<(bool Succeeded, string UserId, IEnumerable<string> Errors)> ValidateCredentialsAsync(
        string phoneNumber,
        string password,
        CancellationToken cancellationToken = default);

    Task<bool> IsPhoneRegisteredAsync(
        string phoneNumber,
        string? ignoreUserId = null,
        CancellationToken cancellationToken = default);

    Task<bool> IsUserNameRegisteredAsync(
        string userName,
        string? ignoreUserId = null,
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

    Task<bool> IsEmailRegisteredAsync(
        string email,
        string? ignoreUserId = null,
        CancellationToken cancellationToken = default);

    Task<bool> MustChangePasswordAsync(string userId, CancellationToken cancellationToken = default);
    Task CompleteFirstLoginAsync(string userId, string resetToken, string newPassword, CancellationToken cancellationToken = default);

    // Employee & Settings User Operations
    Task<string> CreateUserAsync(string fullName, string userName, string email, string mobileNumber, string role, string password, string? tenantId, CancellationToken cancellationToken = default);
    Task UpdateUserAsync(string userId, string fullName, string userName, string email, string mobileNumber, CancellationToken cancellationToken = default);
    Task ChangeRoleAsync(string userId, string newRole, CancellationToken cancellationToken = default);
    Task SetStatusAsync(string userId, bool isActive, CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> GetRolesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);
    Task<Dictionary<string, UserSummaryDto>> GetUsersSummaryAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);
    Task<UserSummaryDto?> GetUserSummaryAsync(string userId, CancellationToken cancellationToken = default);
    Task EnsureRetainSuperAdminAsync(IEnumerable<string> activeIdentityUserIds, string targetCurrentRole, bool deactivating, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default);

    // Settings Security & User Operations
    Task<SecurityUserSettingsDto> GetSecurityUserSettingsAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserSummaryDto>> GetTenantUsersAsync(
        string tenantId,
        string? excludeUserId = null,
        CancellationToken cancellationToken = default);
    Task UpdateSecurityUserSettingsAsync(string userId, bool enableTwoFactor, bool loginAlertsEnabled, bool logoutOtherDevices, CancellationToken cancellationToken = default);
    Task BanTenantUserAsync(string currentUserId, string targetUserId, CancellationToken cancellationToken = default);
    Task DeleteTenantUserAsync(string currentUserId, string targetUserId, CancellationToken cancellationToken = default);
    Task DeactivateAccountAsync(string userId, CancellationToken cancellationToken = default);
}

public record SecurityUserSettingsDto(string Id, bool TwoFactorEnabled, bool LoginAlertsEnabled, string? PhoneNumber);
