using Microsoft.EntityFrameworkCore;
using Operia.Domain.Entities;
using Operia.Domain.Interfaces;
using Operia.Infrastructure.Persistence;

namespace Operia.Infrastructure.Repositories;

public sealed class SubscriptionPlanRepository : ISubscriptionPlanRepository
{
    private readonly ApplicationDbContext _context;

    public SubscriptionPlanRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<SubscriptionPlan?> GetActiveByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
        => _context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, cancellationToken);

    public async Task<IReadOnlyList<SubscriptionPlan>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
        => await _context.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.MonthlyPrice)
            .ToListAsync(cancellationToken);
}
