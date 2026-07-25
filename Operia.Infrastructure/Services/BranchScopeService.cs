using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Interfaces;

namespace Operia.Infrastructure.Services;

public sealed class BranchScopeService : IBranchScope
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public BranchScopeService(IApplicationDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyCollection<string>> GetAllowedBranchIdsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_currentUserService.UserId) || !string.Equals(userId, _currentUserService.UserId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Requested user ID does not match the authenticated user.");
        }

        var tenantId = _currentUserService.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            throw new UnauthorizedAccessException("The current user does not have a tenant.");
        }

        if (_currentUserService.IsInRole(Roles.SuperAdmin))
        {
            var allBranchIds = await _db.Branches
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            return allBranchIds;
        }

        var assignedBranchIds = await _db.UserBranches
            .AsNoTracking()
            .Where(ub => ub.TenantId == tenantId)
            .Join(
                _db.Employees.AsNoTracking().Where(e => e.TenantId == tenantId && e.IdentityUserId == userId && e.IsActive),
                ub => ub.EmployeeId,
                e => e.Id,
                (ub, e) => ub.BranchId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return assignedBranchIds;
    }
}
