using Operia.Application.Auth.DTOs;

namespace Operia.Application.Common.Interfaces;

public interface ITokenService
{
    Task<AuthResponseDto> GenerateTokensAsync(string userId, CancellationToken cancellationToken = default);

    Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAllRefreshTokensAsync(string userId, CancellationToken cancellationToken = default);
}
