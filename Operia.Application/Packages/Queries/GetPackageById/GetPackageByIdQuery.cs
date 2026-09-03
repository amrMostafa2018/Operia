using MediatR;
using Operia.Application.Packages.DTOs;

namespace Operia.Application.Packages.Queries.GetPackageById;

public sealed record GetPackageByIdQuery(string Id) : IRequest<PackageDetailDto>;
