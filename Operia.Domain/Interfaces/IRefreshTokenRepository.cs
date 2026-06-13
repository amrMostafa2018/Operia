using Operia.Domain.Entities;

namespace Operia.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetActiveByTokenAsync(string token, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);

    void Revoke(RefreshToken refreshToken, DateTime revokedAt);
}
