using Microsoft.EntityFrameworkCore;
using Operia.Domain.Entities;
using Operia.Domain.Interfaces;
using Operia.Infrastructure.Persistence;

namespace Operia.Infrastructure.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ApplicationDbContext _context;

    public RefreshTokenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<RefreshToken?> GetActiveByTokenAsync(string token, CancellationToken cancellationToken = default)
        => _context.RefreshTokens
            .FirstOrDefaultAsync(x => x.Token == token, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
        => await _context.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

    public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
        => _context.RefreshTokens.AddAsync(refreshToken, cancellationToken).AsTask();

    public void Revoke(RefreshToken refreshToken, DateTime revokedAt)
        => refreshToken.RevokedAt = revokedAt;
}
