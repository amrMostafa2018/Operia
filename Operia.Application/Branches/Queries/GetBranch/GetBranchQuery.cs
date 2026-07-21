namespace Operia.Application.Branches.Queries.GetBranch;

public sealed record GetBranchQuery(string Id) : MediatR.IRequest<BranchDto>;
