using Microsoft.EntityFrameworkCore;
using Operia.Application.Branches;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.Domain.Exceptions;

namespace Operia.Application.Branches.Queries.GetBranch;

public sealed class GetBranchHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser) : MediatR.IRequestHandler<GetBranchQuery, BranchDto>
{
    public async Task<BranchDto> Handle(
        GetBranchQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = BranchMapper.RequireTenant(currentUser);
        var branch = await db.Branches.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.Id && item.TenantId == tenantId,
            cancellationToken) ?? throw new NotFoundException(nameof(Branch), request.Id);

        return BranchMapper.ToDto(branch);
    }
}
