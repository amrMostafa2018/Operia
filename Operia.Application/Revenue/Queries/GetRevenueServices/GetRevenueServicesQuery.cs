using MediatR;

namespace Operia.Application.Revenue.Queries.GetRevenueServices;

public sealed record GetRevenueServicesQuery : IRequest<IReadOnlyList<RevenueServiceDto>>;

public sealed record RevenueServiceDto(string Id, string Name);
