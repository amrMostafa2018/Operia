using Microsoft.EntityFrameworkCore;
using Operia.Domain.Entities;
using Operia.Domain.Interfaces;
using Operia.Infrastructure.Persistence;

namespace Operia.Infrastructure.Repositories;

public sealed class TenantSubscriptionRepository : ITenantSubscriptionRepository
{
    private readonly ApplicationDbContext _context;

    public TenantSubscriptionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<TenantSubscription?> GetByIdWithDetailsAsync(
        string id,
        CancellationToken cancellationToken = default)
        => _context.TenantSubscriptions
            .Include(s => s.Tenant)
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task AddAsync(TenantSubscription subscription, CancellationToken cancellationToken = default)
        => _context.TenantSubscriptions.AddAsync(subscription, cancellationToken).AsTask();
}
