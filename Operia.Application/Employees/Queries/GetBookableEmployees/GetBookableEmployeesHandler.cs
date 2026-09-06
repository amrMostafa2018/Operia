using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Interfaces;
using Operia.Application.Employees.Common;

namespace Operia.Application.Employees.Queries.GetBookableEmployees;

public sealed class GetBookableEmployeesHandler
    : IRequestHandler<GetBookableEmployeesQuery, IReadOnlyList<BookableEmployeeDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;

    public GetBookableEmployeesHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUserService,
        IIdentityService identityService)
    {
        _db = db;
        _currentUserService = currentUserService;
        _identityService = identityService;
    }

    public async Task<IReadOnlyList<BookableEmployeeDto>> Handle(
        GetBookableEmployeesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = EmployeeHandlerHelpers.RequireTenant(_currentUserService);
        if (string.IsNullOrWhiteSpace(request.BranchId))
        {
            return [];
        }

        var employees = await _db.Employees
            .AsNoTracking()
            .Where(employee => employee.TenantId == tenantId && employee.IsActive)
            .Where(employee => employee.UserBranches.Any(branch => branch.BranchId == request.BranchId))
            .OrderBy(employee => employee.Code)
            .Select(employee => new
            {
                employee.Id,
                employee.IdentityUserId,
                employee.Code,
                employee.FullName,
                employee.PhotoUrl,
                employee.Specialty,
                employee.JobTitle
            })
            .ToListAsync(cancellationToken);

        var roles = await _identityService.GetRolesAsync(
            employees.Select(employee => employee.IdentityUserId),
            cancellationToken);

        var staff = employees
            .Where(employee =>
                string.Equals(
                    roles.GetValueOrDefault(employee.IdentityUserId),
                    Roles.Staff,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        var staffIds = staff.Select(employee => employee.Id).ToList();
        var workingDays = await _db.EmployeeWorkingDays
            .AsNoTracking()
            .Where(day =>
                day.TenantId == tenantId &&
                day.BranchId == request.BranchId &&
                staffIds.Contains(day.EmployeeId))
            .ToListAsync(cancellationToken);

        var daysByEmployee = workingDays
            .GroupBy(day => day.EmployeeId)
            .ToDictionary(group => group.Key, group => group.AsEnumerable());

        return staff
            .Select(employee =>
            {
                daysByEmployee.TryGetValue(employee.Id, out var employeeDays);

                return new BookableEmployeeDto(
                    employee.Id,
                    employee.Code,
                    employee.FullName,
                    employee.PhotoUrl,
                    employee.Specialty,
                    employee.JobTitle,
                    EmployeeScheduleHelpers.ToWeek(employeeDays ?? []));
            })
            .ToList();
    }
}
