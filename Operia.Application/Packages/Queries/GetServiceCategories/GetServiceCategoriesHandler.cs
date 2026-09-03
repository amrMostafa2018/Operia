using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.DTOs;

namespace Operia.Application.Packages.Queries.GetServiceCategories;

public sealed class GetServiceCategoriesHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<GetServiceCategoriesQuery, IReadOnlyList<ServiceCategoryDto>>
{
    public async Task<IReadOnlyList<ServiceCategoryDto>> Handle(
        GetServiceCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = PackageMapper.RequireTenant(currentUser);

        var categories = await db.ServiceCategories
            .AsNoTracking()
            .Where(category => category.TenantId == tenantId && category.IsActive)
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);

        return categories.Select(PackageMapper.ToDto).ToList();
    }
}
