using Microsoft.EntityFrameworkCore;
using Operia.Application.Onboarding.Commands.SetupBusiness;
using Operia.Domain.Entities;
using Operia.Infrastructure.Persistence;

namespace Operia.Infrastructure.Repositories;

/// <summary>
/// Infrastructure implementation of the documented owner-scoped onboarding gallery lookup.
/// </summary>
public sealed class OnboardingBusinessGalleryStore : IOnboardingBusinessGalleryStore
{
    private readonly ApplicationDbContext _context;

    public OnboardingBusinessGalleryStore(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<BusinessGallery?> GetMainImageForOwnerScopeAsync(
        string businessId,
        string tenantId,
        CancellationToken cancellationToken = default)
        => _context.BusinessGalleries
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                gallery => gallery.BusinessId == businessId
                           && gallery.IsMainImage
                           && gallery.Business != null
                           && gallery.Business.TenantId == tenantId,
                cancellationToken);
}
