using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Interfaces;

namespace Operia.Application.Onboarding.Common;

internal static class OnboardingHandlerHelpers
{
    public static async Task<TenantUploadContextDto> ResolveUploadTenantContextAsync(
        ITenantRepository tenantRepository,
        string userId,
        string? currentTenantId,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(currentTenantId))
            return new TenantUploadContextDto(currentTenantId, false);

        var existingTenant = await tenantRepository.GetByOwnerUserIdWithDetailsAsync(
            userId,
            cancellationToken);

        if (existingTenant is not null)
            return new TenantUploadContextDto(existingTenant.Id, false);

        return new TenantUploadContextDto(Guid.NewGuid().ToString(), true);
    }
}
