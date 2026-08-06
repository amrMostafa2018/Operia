using Operia.Application.Common.Exceptions;

namespace Operia.Application.Branches.Commands.DeleteBranch;

public interface IBranchDependencyProbe
{
    Task<BranchDependencyInfo?> GetDependencyAsync(
        string tenantId,
        string branchId,
        CancellationToken cancellationToken);
}
