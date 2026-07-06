using Operia.Domain.Entities;

namespace Operia.Domain.Interfaces;

public interface IPlatformRevenueRepository
{
    Task<PlatformRevenue?> GetLatestUnconfirmedByTenantIdAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task AddAsync(PlatformRevenue revenue, CancellationToken cancellationToken = default);
}
