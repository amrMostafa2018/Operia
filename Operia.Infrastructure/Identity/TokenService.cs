using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Interfaces;
using Operia.Application.Auth.DTOs;
using Operia.Domain.Exceptions;
using Operia.Domain.Entities;
using Operia.Domain.Interfaces;
using Operia.Infrastructure.Options;
using Operia.SharedKernel.Interfaces;

namespace Operia.Infrastructure.Identity;

public sealed class TokenService : ITokenService
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly JwtSettings _jwtSettings;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public TokenService(
        IRefreshTokenRepository refreshTokenRepository,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<JwtSettings> jwtSettings,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtSettings = jwtSettings.Value;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResponseDto> GenerateTokensAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);

        var accessToken = await GenerateAccessTokenAsync(user);
        var refreshToken = await CreateRefreshTokenAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponseDto(
            accessToken.Token,
            refreshToken.Token,
            accessToken.ExpiresAt);
    }

    public async Task<AuthResponseDto?> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var storedToken = await _refreshTokenRepository.GetActiveByTokenAsync(refreshToken, cancellationToken);

        if (storedToken is null || !storedToken.IsActive)
            return null;

        _refreshTokenRepository.Revoke(storedToken, _dateTimeProvider.UtcNow);

        var user = await _userManager.FindByIdAsync(storedToken.UserId)
            ?? throw new NotFoundException(nameof(ApplicationUser), storedToken.UserId);

        var accessToken = await GenerateAccessTokenAsync(user);
        var newRefreshToken = await CreateRefreshTokenAsync(user, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponseDto(
            accessToken.Token,
            newRefreshToken.Token,
            accessToken.ExpiresAt);
    }

    public async Task RevokeAllRefreshTokensAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _refreshTokenRepository.GetActiveByUserIdAsync(userId, cancellationToken);

        foreach (var token in tokens)
        {
            _refreshTokenRepository.Revoke(token, _dateTimeProvider.UtcNow);
        }
    }

    private async Task<(string Token, DateTime ExpiresAt)> GenerateAccessTokenAsync(ApplicationUser user)
    {
        var expiresAt = _dateTimeProvider.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.MobilePhone, user.PhoneNumber ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("security_stamp", user.SecurityStamp ?? string.Empty)
        };

        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        if (user.TenantId is not null)
        {
            claims.Add(new Claim("tenant_id", user.TenantId));
        }

        claims.AddRange(await GetPermissionClaimsAsync(roles));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private async Task<IReadOnlyList<Claim>> GetPermissionClaimsAsync(IList<string> roles)
    {
        var claims = new List<Claim>();

        foreach (var roleName in roles)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role is null)
                continue;

            claims.AddRange(
                (await _roleManager.GetClaimsAsync(role))
                    .Where(c => c.Type == Permissions.ClaimType));
        }

        return claims
            .GroupBy(c => c.Value, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();
    }

    private async Task<RefreshToken> CreateRefreshTokenAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var refreshToken = new RefreshToken
        {
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt = _dateTimeProvider.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
            CreatedAt = _dateTimeProvider.UtcNow,
            UserId = user.Id
        };

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        return refreshToken;
    }
}
