namespace Operia.Application.Common.Interfaces;

public interface IAdminTenantService
{
    Task ActivateSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    Task ApproveAddBalancePlatformAsync(
        string revenueId,
        CancellationToken cancellationToken = default);
}
