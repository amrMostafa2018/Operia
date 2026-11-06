using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Revenue.Queries.GetRevenueServices;

public sealed class GetRevenueServicesHandler(
    IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetRevenueServicesQuery, IReadOnlyList<RevenueServiceDto>>
{
    public async Task<IReadOnlyList<RevenueServiceDto>> Handle(
        GetRevenueServicesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        return await db.Packages.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new RevenueServiceDto(x.Id, x.Name))
            .ToListAsync(cancellationToken);
    }
}
