using Microsoft.AspNetCore.Identity;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Exceptions;
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
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null || !await _userManager.CheckPasswordAsync(user, password))
        {
            return (false, string.Empty, ["Invalid email or password."]);
        }

        return (true, user.Id, []);
    }

    public async Task LogoutAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);

        await _userManager.UpdateSecurityStampAsync(user);
        await _tokenService.RevokeAllRefreshTokensAsync(userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
