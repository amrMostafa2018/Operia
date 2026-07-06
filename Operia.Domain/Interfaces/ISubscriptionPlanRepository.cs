using Operia.Domain.Entities;

namespace Operia.Domain.Interfaces;

public interface ISubscriptionPlanRepository
{
    Task<SubscriptionPlan?> GetActiveByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionPlan>> GetAllActiveAsync(
        CancellationToken cancellationToken = default);
}
