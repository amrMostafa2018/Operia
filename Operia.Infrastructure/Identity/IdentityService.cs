using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Exceptions;
using Operia.SharedKernel.Errors;
using Operia.Application.Common.PhoneNumbers;
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
}
