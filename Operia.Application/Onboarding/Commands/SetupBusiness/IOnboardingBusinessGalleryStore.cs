using Operia.Domain.Entities;

namespace Operia.Application.Onboarding.Commands.SetupBusiness;

/// <summary>
/// Provides the owner-scoped gallery lookup needed before a tenant context exists during onboarding.
/// </summary>
public interface IOnboardingBusinessGalleryStore
{
    Task<BusinessGallery?> GetMainImageForOwnerScopeAsync(
        string businessId,
        string tenantId,
        CancellationToken cancellationToken = default);
}
