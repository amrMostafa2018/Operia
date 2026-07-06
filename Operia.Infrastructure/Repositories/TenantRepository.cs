using Microsoft.EntityFrameworkCore;
using Operia.Domain.Entities;
using Operia.Domain.Interfaces;
using Operia.Infrastructure.Persistence;

namespace Operia.Infrastructure.Repositories;

public sealed class TenantRepository : ITenantRepository
{
    private readonly ApplicationDbContext _context;

    public TenantRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Tenant?> GetByOwnerUserIdWithDetailsAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
        => _context.Tenants
            .Include(t => t.Businesses)
            .Include(t => t.Subscriptions)
            .FirstOrDefaultAsync(t => t.OwnerUserId == ownerUserId, cancellationToken);

    public Task<Tenant?> GetByOwnerUserIdForStatusAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
        => _context.Tenants
            .AsNoTracking()
            .Include(t => t.Businesses)
            .Include(t => t.Subscriptions)
            .FirstOrDefaultAsync(t => t.OwnerUserId == ownerUserId, cancellationToken);

    public Task<Tenant?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => _context.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
        => _context.Tenants.AddAsync(tenant, cancellationToken).AsTask();
}
