using MediatR;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Packages.Queries.GetAvailablePackageStaff;

public sealed class GetAvailablePackageStaffHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IIdentityService identity) : IRequestHandler<GetAvailablePackageStaffQuery, IReadOnlyList<AvailablePackageStaffDto>>
{
    public Task<IReadOnlyList<AvailablePackageStaffDto>> Handle(
        GetAvailablePackageStaffQuery request,
        CancellationToken cancellationToken) =>
        PackageStaffEligibility.ListAsync(
            db, currentUser, branchScope, identity, PackageMapper.RequireTenant(currentUser), cancellationToken);
}
