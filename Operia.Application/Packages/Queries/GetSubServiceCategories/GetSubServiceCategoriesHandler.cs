using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.DTOs;

namespace Operia.Application.Packages.Queries.GetSubServiceCategories;

public sealed class GetSubServiceCategoriesHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<GetSubServiceCategoriesQuery, IReadOnlyList<SubServiceCategoryDto>>
{
    public async Task<IReadOnlyList<SubServiceCategoryDto>> Handle(
        GetSubServiceCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = PackageMapper.RequireTenant(currentUser);
        var query = db.SubServiceCategories
            .AsNoTracking()
            .Where(category => category.TenantId == tenantId && category.IsActive);

        if (!string.IsNullOrWhiteSpace(request.ServiceCategoryId))
        {
            query = query.Where(category => category.ServiceCategoryId == request.ServiceCategoryId);
        }

        var categories = await query
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);

        return categories.Select(PackageMapper.ToDto).ToList();
    }
}
