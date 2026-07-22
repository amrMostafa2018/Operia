using Operia.Application.Onboarding.DTOs;

namespace Operia.Application.Common.Interfaces;

public interface IPlatformService
{
    Task<AddBalancePlatformResultDto> AddBalancePlatformAsync(
        string userId,
        decimal amount,
        string screenShotUrl,
        CancellationToken cancellationToken = default);

    Task ApproveAddBalancePlatformAsync(
        string revenueId,
        CancellationToken cancellationToken = default);

    Task<PendingAddBalancePlatformDto?> GetPendingAddBalancePlatformAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}
