namespace Operia.Application.Branches.Queries.ListBranches;

public sealed record BranchListResult(
    IReadOnlyList<BranchDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    int TotalTenantCount);
