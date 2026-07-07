using Microsoft.EntityFrameworkCore;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
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

    public async Task<decimal> GetActiveSubscriptionSpendTotalAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var sum = await _context.TenantSubscriptions
            .Where(s => s.TenantId == tenantId && s.Status == SubscriptionStatus.Active)
            .SumAsync(s => (decimal?)s.Amount, cancellationToken);

        return sum ?? 0;
    }
}
