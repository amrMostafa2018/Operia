using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.PhoneNumbers;
using Operia.Domain.Exceptions;
using Operia.SharedKernel.Errors;

namespace Operia.Infrastructure.Identity;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
    }

    public async Task<(bool Succeeded, string UserId, IEnumerable<string> Errors)> ValidateCredentialsAsync(
        string phoneNumber,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(
            u => u.PhoneNumber == phoneNumber,
            cancellationToken);

        if (user is null || user.LockoutEnd > DateTimeOffset.UtcNow || !await _userManager.CheckPasswordAsync(user, password))
        {
            return (false, string.Empty, ["Invalid phone number or password."]);
        }

        return (true, user.Id, []);
    }

    public async Task<bool> IsPhoneRegisteredAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        return await _userManager.Users
            .AnyAsync(u => u.PhoneNumber == phoneNumber, cancellationToken);
    }

    public async Task LogoutAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);

        await _userManager.UpdateSecurityStampAsync(user);
        await _tokenService.RevokeAllRefreshTokensAsync(userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetUserIdByPhoneAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber, cancellationToken);

        return user?.Id;
    }

    public async Task<string> GeneratePasswordResetTokenAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);

        return await _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task ResetPasswordAsync(
        string userId,
        string resetToken,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);

        var result = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);

        if (!result.Succeeded)
        {
            throw new ValidationException(
                result.Errors.Select(e =>
                    ValidationFailureFactory.Create("password", ApiErrorCodes.Auth.IdentityError, e.Description)));
        }

        await _userManager.UpdateSecurityStampAsync(user);
        await _tokenService.RevokeAllRefreshTokensAsync(userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetUserTenantIdAsync(
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);

        user.TenantId = tenantId;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new ValidationException(
                result.Errors.Select(e =>
                    ValidationFailureFactory.Create("tenantId", ApiErrorCodes.Auth.IdentityError, e.Description)));
        }
    }

    public async Task<bool> IsEmailRegisteredAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = _userManager.NormalizeEmail(email.Trim());
        return await _userManager.Users.AnyAsync(
            user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);
    }

    public async Task<bool> MustChangePasswordAsync(string userId, CancellationToken cancellationToken = default)
        => await _userManager.Users.Where(x => x.Id == userId).Select(x => x.MustChangePassword).SingleAsync(cancellationToken);

    public async Task CompleteFirstLoginAsync(string userId, string resetToken, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException(nameof(ApplicationUser), userId);
        if (!user.MustChangePassword) throw new ConflictException("First-login password replacement is already complete.");
        var result = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);
        if (!result.Succeeded) throw new ValidationException(result.Errors.Select(x => ValidationFailureFactory.Create("newPassword", ApiErrorCodes.Auth.IdentityError, x.Description)));
        user.MustChangePassword = false;
        await _userManager.UpdateSecurityStampAsync(user);
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded) throw new ValidationException(update.Errors.Select(x => ValidationFailureFactory.Create("newPassword", ApiErrorCodes.Auth.IdentityError, x.Description)));
        await _tokenService.RevokeAllRefreshTokensAsync(userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> CreateUserAsync(string fullName, string userName, string email, string mobileNumber, string role, string password, string? tenantId, CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser
        {
            FullName = fullName.Trim(),
            UserName = userName.Trim(),
            Email = email.Trim(),
            PhoneNumber = PhoneNumberHelper.ToE164(mobileNumber),
            TenantId = tenantId,
            MustChangePassword = true,
            EmailConfirmed = true,
            PhoneNumberConfirmed = true
        };
        var result = await _userManager.CreateAsync(user, password);
        EnsureIdentityResult(result, "identity");
        var roleResult = await _userManager.AddToRoleAsync(user, role);
        EnsureIdentityResult(roleResult, "role");
        return user.Id;
    }

    public async Task UpdateUserAsync(string userId, string fullName, string userName, string email, string mobileNumber, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException(nameof(ApplicationUser), userId);
        user.FullName = fullName.Trim();
        user.UserName = userName.Trim();
        user.Email = email.Trim();
        user.PhoneNumber = PhoneNumberHelper.ToE164(mobileNumber);
        EnsureIdentityResult(await _userManager.UpdateAsync(user), "identity");
    }

    public async Task ChangeRoleAsync(string userId, string newRole, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException(nameof(ApplicationUser), userId);
        var oldRoles = await _userManager.GetRolesAsync(user);
        foreach (var r in oldRoles)
        {
            EnsureIdentityResult(await _userManager.RemoveFromRoleAsync(user, r), "role");
        }
        EnsureIdentityResult(await _userManager.AddToRoleAsync(user, newRole), "role");
    }

    public async Task SetStatusAsync(string userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException(nameof(ApplicationUser), userId);
        user.LockoutEnabled = true;
        user.LockoutEnd = isActive ? null : DateTimeOffset.MaxValue;
        EnsureIdentityResult(await _userManager.UpdateSecurityStampAsync(user), "status");
        EnsureIdentityResult(await _userManager.UpdateAsync(user), "status");
        await _tokenService.RevokeAllRefreshTokensAsync(user.Id, cancellationToken);
    }

    public async Task EnsureIdentityUniqueAsync(string? ignoreUserId, string userName, string email, string mobileNumber, CancellationToken cancellationToken = default)
    {
        var normalizedName = _userManager.NormalizeName(userName.Trim());
        var normalizedEmail = _userManager.NormalizeEmail(email.Trim());
        var mobile = PhoneNumberHelper.ToE164(mobileNumber);
        if (await _userManager.Users.AnyAsync(x => x.Id != ignoreUserId && (x.NormalizedUserName == normalizedName || x.NormalizedEmail == normalizedEmail || x.PhoneNumber == mobile), cancellationToken))
            throw new ConflictException("Username, email, or mobile number is already registered.");
    }

    public async Task<Dictionary<string, string>> GetRolesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>();
        foreach (var id in userIds.Distinct())
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is not null) result[id] = (await _userManager.GetRolesAsync(user)).SingleOrDefault() ?? string.Empty;
        }
        return result;
    }

    public async Task<Dictionary<string, UserSummaryDto>> GetUsersSummaryAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToListAsync(cancellationToken);
        return users.ToDictionary(x => x.Id, x => new UserSummaryDto(x.Id, x.UserName ?? string.Empty, x.Email ?? string.Empty, x.PhoneNumber ?? string.Empty, x.LockoutEnd > DateTimeOffset.UtcNow));
    }

    public async Task<UserSummaryDto?> GetUserSummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        return user is null ? null : new UserSummaryDto(user.Id, user.UserName ?? string.Empty, user.Email ?? string.Empty, user.PhoneNumber ?? string.Empty, user.LockoutEnd > DateTimeOffset.UtcNow);
    }

    public async Task EnsureRetainSuperAdminAsync(IEnumerable<string> activeIdentityUserIds, string targetCurrentRole, bool deactivating, CancellationToken cancellationToken = default)
    {
        if (targetCurrentRole != Roles.SuperAdmin || !deactivating) return;
        foreach (var id in activeIdentityUserIds)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is not null && await _userManager.IsInRoleAsync(user, Roles.SuperAdmin)) return;
        }
        throw new ConflictException("The tenant must retain at least one active Super Admin.");
    }

    public async Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException(nameof(ApplicationUser), userId);
        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        EnsureIdentityResult(result, "newPassword");
        await _userManager.UpdateSecurityStampAsync(user);
        await _tokenService.RevokeAllRefreshTokensAsync(userId, cancellationToken);
    }

    public async Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException(nameof(ApplicationUser), userId);
        EnsureIdentityResult(await _userManager.DeleteAsync(user), "user");
    }

    public async Task<SecurityUserSettingsDto> GetSecurityUserSettingsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException(nameof(ApplicationUser), userId);
        return new SecurityUserSettingsDto(user.Id, user.TwoFactorEnabled, user.LoginAlertsEnabled, user.PhoneNumber);
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetTenantUsersAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var list = await _userManager.Users.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.UserName)
            .Select(x => new UserSummaryDto(x.Id, x.UserName ?? x.Email ?? x.PhoneNumber ?? "User", x.Email ?? string.Empty, x.PhoneNumber ?? string.Empty, x.LockoutEnd.HasValue && x.LockoutEnd > DateTimeOffset.UtcNow))
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task UpdateSecurityUserSettingsAsync(string userId, bool enableTwoFactor, bool loginAlertsEnabled, bool logoutOtherDevices, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException(nameof(ApplicationUser), userId);
        user.TwoFactorEnabled = enableTwoFactor;
        user.LoginAlertsEnabled = loginAlertsEnabled;
        EnsureIdentityResult(await _userManager.UpdateAsync(user), "security");
        if (logoutOtherDevices)
        {
            EnsureIdentityResult(await _userManager.UpdateSecurityStampAsync(user), "security");
        }
    }

    public async Task BanTenantUserAsync(string currentUserId, string targetUserId, CancellationToken cancellationToken = default)
    {
        if (currentUserId == targetUserId)
            throw new ConflictException("You cannot modify your own account.");
        var currentUser = await _userManager.FindByIdAsync(currentUserId) ?? throw new NotFoundException(nameof(ApplicationUser), currentUserId);
        var targetUser = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == targetUserId && x.TenantId == currentUser.TenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(ApplicationUser), targetUserId);
        await _userManager.SetLockoutEnabledAsync(targetUser, true);
        await _userManager.SetLockoutEndDateAsync(targetUser, DateTimeOffset.UtcNow.AddYears(100));
        await _userManager.UpdateSecurityStampAsync(targetUser);
    }

    public async Task DeleteTenantUserAsync(string currentUserId, string targetUserId, CancellationToken cancellationToken = default)
    {
        if (currentUserId == targetUserId)
            throw new ConflictException("You cannot modify your own account.");
        var currentUser = await _userManager.FindByIdAsync(currentUserId) ?? throw new NotFoundException(nameof(ApplicationUser), currentUserId);
        var targetUser = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == targetUserId && x.TenantId == currentUser.TenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(ApplicationUser), targetUserId);
        EnsureIdentityResult(await _userManager.DeleteAsync(targetUser), "user");
    }

    public async Task DeactivateAccountAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException(nameof(ApplicationUser), userId);
        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        await _userManager.UpdateSecurityStampAsync(user);
    }

    private static void EnsureIdentityResult(IdentityResult result, string field)
    {
        if (!result.Succeeded)
            throw new ValidationException(result.Errors.Select(x => ValidationFailureFactory.Create(field, ApiErrorCodes.Auth.IdentityError, x.Description)));
    }
}
