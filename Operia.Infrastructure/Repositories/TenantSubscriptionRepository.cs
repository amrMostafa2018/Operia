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

    public Task<TenantSubscription?> GetByIdForPlatformAsync(
        string id,
        CancellationToken cancellationToken = default)
        => _context.TenantSubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.Tenant)
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TenantSubscription>> GetFilteredByTenantAsync(
        string tenantId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? planCode,
        SubscriptionStatus? status,
        CancellationToken cancellationToken = default)
        => await BuildFilteredQuery(tenantId, dateFrom, dateTo, planCode, status)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<TenantSubscription> Items, int TotalCount)> GetFilteredByTenantPagedAsync(
        string tenantId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? planCode,
        SubscriptionStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildFilteredQuery(tenantId, dateFrom, dateTo, planCode, status);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task AddAsync(TenantSubscription subscription, CancellationToken cancellationToken = default)
        => _context.TenantSubscriptions.AddAsync(subscription, cancellationToken).AsTask();

    private IQueryable<TenantSubscription> BuildFilteredQuery(
        string tenantId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? planCode,
        SubscriptionStatus? status)
    {
        var query = _context.TenantSubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.TenantId == tenantId);

        if (dateFrom.HasValue)
        {
            query = query.Where(s => s.StartDate >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(s => s.StartDate <= dateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(planCode))
        {
            query = query.Where(s => s.Plan != null && s.Plan.Code == planCode);
        }

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        return query.OrderByDescending(s => s.StartDate ?? DateOnly.MinValue)
            .ThenByDescending(s => s.CreatedAt);
    }
}
