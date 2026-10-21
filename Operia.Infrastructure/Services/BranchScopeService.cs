using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Infrastructure.Services;

/// <summary>Resolves the branches a user may access within the authenticated tenant.</summary>
public sealed class BranchScopeService : IBranchScope
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public BranchScopeService(IApplicationDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    /// <summary>Returns tenant branches permitted to the authenticated user; rejects a mismatched user ID.</summary>
    public async Task<IReadOnlyCollection<string>> GetAllowedBranchIdsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_currentUserService.UserId) || !string.Equals(userId, _currentUserService.UserId, StringComparison.Ordinal))
        {
            throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantAccessDenied);
        }

        var tenantId = _currentUserService.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
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
