using MediatR;

namespace Operia.Application.Revenue.Queries.GetRevenueBranches;

public sealed record GetRevenueBranchesQuery : IRequest<IReadOnlyList<RevenueBranchDto>>;

public sealed record RevenueBranchDto(string Id, string Name);
