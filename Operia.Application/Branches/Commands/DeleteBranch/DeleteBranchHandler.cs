using Microsoft.EntityFrameworkCore;
using Operia.Application.Branches;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.Domain.Exceptions;

namespace Operia.Application.Branches.Commands.DeleteBranch;

public sealed class DeleteBranchHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IEnumerable<IBranchDependencyProbe> probes) : MediatR.IRequestHandler<DeleteBranchCommand>
{
    public async Task Handle(
        DeleteBranchCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = BranchMapper.RequireTenant(currentUser);
        var branch = await db.Branches.SingleOrDefaultAsync(
            item => item.Id == request.Id && item.TenantId == tenantId,
            cancellationToken) ?? throw new NotFoundException(nameof(Branch), request.Id);
        var dependencies = new List<BranchDependencyInfo>();

        foreach (var probe in probes)
        {
            var dependency = await probe.GetDependencyAsync(
                tenantId,
                branch.Id,
                cancellationToken);

            if (dependency is { Count: > 0 })
            {
                dependencies.Add(dependency);
            }
        }

        if (dependencies.Count > 0)
        {
            throw new ConflictException(
                "This branch has related operational data.",
                dependencies);
        }

        db.Branches.Remove(branch);
        BranchMapper.AddAudit(db, currentUser, tenantId, "BranchDeleted", branch, null);
        await db.SaveChangesAsync(cancellationToken);
    }
}
