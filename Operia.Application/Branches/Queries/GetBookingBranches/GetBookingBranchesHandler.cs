using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Branches.Queries.GetBookingBranches;

/// <summary>Returns branches the current user may use for bookings within the current tenant.</summary>
public sealed class GetBookingBranchesHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope)
    : IRequestHandler<GetBookingBranchesQuery, IReadOnlyList<BookingBranchDto>>
{
    /// <summary>Returns only tenant branches allowed by the caller's branch scope.</summary>
    public async Task<IReadOnlyList<BookingBranchDto>> Handle(
        GetBookingBranchesQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var allowedBranches = await branchScope.GetAllowedBranchIdsAsync(userId, cancellationToken);

        return await db.Branches.AsNoTracking()
            .Where(branch => branch.TenantId == tenantId && allowedBranches.Contains(branch.Id))
            .OrderBy(branch => branch.Name)
            .ThenBy(branch => branch.Id)
            .Select(branch => new BookingBranchDto(branch.Id, branch.Name))
            .ToListAsync(cancellationToken);
    }
}
