using Operia.Domain.Entities;

namespace Operia.Domain.Interfaces;

public interface ITenantRepository
{
    Task<Tenant?> GetByOwnerUserIdWithDetailsAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<Tenant?> GetByOwnerUserIdForStatusAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<Tenant?> GetByIdForStatusAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<Tenant?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
}
