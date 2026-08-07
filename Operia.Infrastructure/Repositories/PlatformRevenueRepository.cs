using Microsoft.EntityFrameworkCore;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
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

    public Task<PlatformRevenue?> GetLatestPendingAddBalancePlatformByTenantIdAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
        => _context.PlatformRevenues
            .Where(r => r.TenantId == tenantId && r.Status == PlatformRevenueStatus.Pending)
            .OrderByDescending(r => r.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<PlatformRevenue?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
        => _context.PlatformRevenues.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<PlatformRevenue?> GetByIdForPlatformAsync(
        string id,
        CancellationToken cancellationToken = default)
        => _context.PlatformRevenues
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task AddAsync(PlatformRevenue revenue, CancellationToken cancellationToken = default)
        => _context.PlatformRevenues.AddAsync(revenue, cancellationToken).AsTask();
}
