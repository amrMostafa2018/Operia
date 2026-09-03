using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.DTOs;
using Operia.Domain.Enums;

namespace Operia.Application.Packages.Queries.GetPackages;

public sealed class GetPackagesHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<GetPackagesQuery, PackageListResultDto>
{
    public async Task<PackageListResultDto> Handle(
        GetPackagesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = PackageMapper.RequireTenant(currentUser);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var pageNumber = Math.Max(request.PageNumber, 1);
        var baseQuery = db.Packages.AsNoTracking().Where(package => package.TenantId == tenantId);

        var activeCount = await baseQuery.CountAsync(
            package => package.Status == PackageStatus.Active,
            cancellationToken);
        var cancelledCount = await baseQuery.CountAsync(
            package => package.Status == PackageStatus.Cancelled,
            cancellationToken);

        IQueryable<Domain.Entities.Package> query = baseQuery;
        var search = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(package => package.Name.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(request.OfferType))
        {
            var offerType = PackageMapper.ParseOfferType(request.OfferType);
            query = query.Where(package => package.OfferType == offerType);
        }

        if (!string.IsNullOrWhiteSpace(request.ServiceCategoryId))
        {
            query = query.Where(package => package.ServiceCategoryId == request.ServiceCategoryId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = PackageMapper.ParseStatus(request.Status);
            query = query.Where(package => package.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var packages = await query
            .Include(package => package.ServiceCategory)
            .OrderByDescending(package => package.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = packages.Select(PackageMapper.ToListItemDto).ToList();

        return new PackageListResultDto(
            items,
            pageNumber,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize),
            activeCount,
            cancelledCount);
    }
}
