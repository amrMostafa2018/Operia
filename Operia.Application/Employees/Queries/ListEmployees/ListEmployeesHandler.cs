using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Employees.Common;
using Operia.Domain.Entities;

namespace Operia.Application.Employees.Queries.ListEmployees;

public sealed class ListEmployeesHandler : IRequestHandler<ListEmployeesQuery, EmployeeListResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;

    public ListEmployeesHandler(
        IApplicationDbContext db,
        IIdentityService identityService,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _identityService = identityService;
        _currentUserService = currentUserService;
    }

    public async Task<EmployeeListResult> Handle(ListEmployeesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = EmployeeHandlerHelpers.RequireTenant(_currentUserService);
        var page = Math.Max(1, request.PageNumber);
        var size = Math.Clamp(request.PageSize, 1, 50);

        var query = _db.Employees.AsNoTracking().Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.FullName.Contains(search) || x.Code.Contains(search));
        }
        if (request.IsActive is not null)
            query = query.Where(x => x.IsActive == request.IsActive);
        if (!string.IsNullOrWhiteSpace(request.BranchId))
            query = query.Where(x => x.UserBranches.Any(b => b.BranchId == request.BranchId));
        if (request.JoiningDate is not null)
            query = query.Where(x => x.JoiningDate == request.JoiningDate);

        var allEmployees = await query.OrderBy(x => x.Code).ToListAsync(cancellationToken);
        var userIds = allEmployees.Select(x => x.IdentityUserId).ToList();
        var roles = await _identityService.GetRolesAsync(userIds, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Role))
            allEmployees = allEmployees.Where(x => string.Equals(roles.GetValueOrDefault(x.IdentityUserId), request.Role.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        var total = allEmployees.Count;
        var totalPages = (int)Math.Ceiling(total / (double)size);
        var paged = allEmployees.Skip((page - 1) * size).Take(size).ToList();
        var items = await EmployeeHandlerHelpers.MapManyAsync(_db, _identityService, paged, roles, cancellationToken);

        var roleCounts = allEmployees
            .GroupBy(x => roles.GetValueOrDefault(x.IdentityUserId) ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => new EmployeeRoleCountDto(x.Key, x.Count()))
            .ToList();

        return new EmployeeListResult(items, page, size, total, totalPages, roleCounts);
    }
}
