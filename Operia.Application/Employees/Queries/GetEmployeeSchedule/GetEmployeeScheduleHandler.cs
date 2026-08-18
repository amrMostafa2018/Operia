using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Employees.Common;

namespace Operia.Application.Employees.Queries.GetEmployeeSchedule;

public sealed class GetEmployeeScheduleHandler : IRequestHandler<GetEmployeeScheduleQuery, EmployeeScheduleDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetEmployeeScheduleHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<EmployeeScheduleDto> Handle(GetEmployeeScheduleQuery request, CancellationToken cancellationToken)
    {
        var tenantId = EmployeeHandlerHelpers.RequireTenant(_currentUserService);
        await EmployeeScheduleHelpers.EnsureEmployeeAsync(_db, tenantId, request.EmployeeId, cancellationToken);

        var assignedBranches = await _db.UserBranches.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.EmployeeId == request.EmployeeId)
            .Select(x => new { x.BranchId, BranchName = x.Branch!.Name })
            .ToListAsync(cancellationToken);

        var days = await _db.EmployeeWorkingDays.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.EmployeeId == request.EmployeeId)
            .ToListAsync(cancellationToken);
        var daysByBranch = days.GroupBy(x => x.BranchId).ToDictionary(x => x.Key, x => x.AsEnumerable());

        var branches = assignedBranches
            .Select(branch =>
            {
                daysByBranch.TryGetValue(branch.BranchId, out var branchDays);
                return new EmployeeBranchScheduleDto(
                    branch.BranchId,
                    branch.BranchName,
                    EmployeeScheduleHelpers.ToWeek(branchDays ?? []));
            })
            .ToList();

        return new EmployeeScheduleDto(branches);
    }
}
