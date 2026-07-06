using Microsoft.EntityFrameworkCore;
using Operia.Domain.Entities;
using Operia.Domain.Interfaces;
using Operia.Infrastructure.Persistence;

namespace Operia.Infrastructure.Repositories;

public sealed class PlatformRevenueRepository : IPlatformRevenueRepository
{
    private readonly ApplicationDbContext _context;

    public PlatformRevenueRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<PlatformRevenue?> GetLatestUnconfirmedByTenantIdAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
        => _context.PlatformRevenues
            .Where(r => r.TenantId == tenantId && r.ConfirmedAt == null)
            .OrderByDescending(r => r.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(PlatformRevenue revenue, CancellationToken cancellationToken = default)
        => _context.PlatformRevenues.AddAsync(revenue, cancellationToken).AsTask();
}
