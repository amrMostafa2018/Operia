using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Revenue.Queries.GetRevenueBranches;

public sealed class GetRevenueBranchesHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope)
    : IRequestHandler<GetRevenueBranchesQuery, IReadOnlyList<RevenueBranchDto>>
{
    public async Task<IReadOnlyList<RevenueBranchDto>> Handle(
        GetRevenueBranchesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var userId = currentUser.UserId
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");
        var allowed = await branchScope.GetAllowedBranchIdsAsync(userId, cancellationToken);
        return await db.Branches.AsNoTracking()
            .Where(x => x.TenantId == tenantId && allowed.Contains(x.Id))
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new RevenueBranchDto(x.Id, x.Name))
            .ToListAsync(cancellationToken);
    }
}
