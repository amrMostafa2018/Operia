using MediatR;

namespace Operia.Application.Packages.Queries.GetAvailablePackageStaff;

public sealed record GetAvailablePackageStaffQuery() : IRequest<IReadOnlyList<AvailablePackageStaffDto>>;
