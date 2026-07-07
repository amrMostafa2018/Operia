using Operia.Domain.Entities;

namespace Operia.Domain.Interfaces;

public interface IPlatformRevenueRepository
{
    Task<PlatformRevenue?> GetLatestPendingAddBalancePlatformByTenantIdAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<PlatformRevenue?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<decimal> GetConfirmedTopUpTotalAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<decimal> GetPendingTopUpTotalAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task AddAsync(PlatformRevenue revenue, CancellationToken cancellationToken = default);
}
