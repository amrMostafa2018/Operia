using Operia.Domain.Entities;

namespace Operia.Domain.Interfaces;

public interface ITenantSubscriptionRepository
{
    Task<TenantSubscription?> GetByIdWithDetailsAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task AddAsync(TenantSubscription subscription, CancellationToken cancellationToken = default);

    Task<decimal> GetActiveSubscriptionSpendTotalAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}
