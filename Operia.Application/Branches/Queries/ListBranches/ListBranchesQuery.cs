namespace Operia.Application.Branches.Queries.ListBranches;

public sealed record ListBranchesQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Search = null,
    string? SortBy = null,
    string? SortDirection = null) : MediatR.IRequest<BranchListResult>;
