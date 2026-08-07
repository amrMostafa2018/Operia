using Operia.Domain.Entities;
using Operia.Domain.Enums;

namespace Operia.Domain.Interfaces;

public interface ITenantSubscriptionRepository
{
    Task<TenantSubscription?> GetByIdWithDetailsAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<TenantSubscription?> GetByIdForPlatformAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantSubscription>> GetFilteredByTenantAsync(
        string tenantId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? planCode,
        SubscriptionStatus? status,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<TenantSubscription> Items, int TotalCount)> GetFilteredByTenantPagedAsync(
        string tenantId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? planCode,
        SubscriptionStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(TenantSubscription subscription, CancellationToken cancellationToken = default);
}
