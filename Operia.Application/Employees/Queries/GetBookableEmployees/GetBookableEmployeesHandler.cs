using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Employees.Common;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Employees.Queries.GetBookableEmployees;

/// <summary>Returns active employees assigned to an accessible booking branch.</summary>
public sealed class GetBookableEmployeesHandler
    : IRequestHandler<GetBookableEmployeesQuery, IReadOnlyList<BookableEmployeeDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly IBranchScope _branchScope;

    /// <summary>Initializes the scoped employee read and branch authorization dependencies.</summary>
    public GetBookableEmployeesHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUserService,
        IIdentityService identityService,
        IBranchScope branchScope)
    {
        _db = db;
        _currentUserService = currentUserService;
        _identityService = identityService;
        _branchScope = branchScope;
    }

    /// <summary>Returns active branch employees, limiting Staff to their own assignment.</summary>
    public async Task<IReadOnlyList<BookableEmployeeDto>> Handle(
        GetBookableEmployeesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = EmployeeHandlerHelpers.RequireTenant(_currentUserService);
        var userId = _currentUserService.UserId
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");
        var allowedBranchIds = await _branchScope.GetAllowedBranchIdsAsync(userId, cancellationToken);
        if (!allowedBranchIds.Contains(request.BranchId, StringComparer.Ordinal))
        {
            throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantAccessDenied, "branchId");
        }

        var employees = await _db.Employees
            .AsNoTracking()
            .Where(employee => employee.TenantId == tenantId && employee.IsActive)
            .Where(employee => employee.UserBranches.Any(branch => branch.BranchId == request.BranchId))
            .Where(employee =>
                !_currentUserService.IsInRole(Roles.Staff) ||
                employee.IdentityUserId == userId)
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
