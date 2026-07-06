namespace Operia.Application.Common.Interfaces;

public interface IAdminTenantService
{
    Task ActivateSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    Task AddTenantBalanceAsync(
        string tenantId,
        decimal amount,
        CancellationToken cancellationToken = default);
}
