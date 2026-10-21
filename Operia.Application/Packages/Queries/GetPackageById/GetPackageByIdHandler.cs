using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.DTOs;

namespace Operia.Application.Packages.Queries.GetPackageById;

public sealed class GetPackageByIdHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<GetPackageByIdQuery, PackageDetailDto>
{
    public async Task<PackageDetailDto> Handle(
        GetPackageByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = PackageMapper.RequireTenant(currentUser);
        var package = await db.Packages
            .AsNoTracking()
            .Include(item => item.ServiceCategory)
            .SingleOrDefaultAsync(
                item => item.Id == request.Id && item.TenantId == tenantId,
                cancellationToken) ?? throw new Domain.Exceptions.NotFoundException(nameof(Domain.Entities.Package), request.Id);

        return PackageMapper.ToDetailDto(package);
    }
}
