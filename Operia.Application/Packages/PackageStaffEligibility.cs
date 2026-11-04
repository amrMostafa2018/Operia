using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Packages;

internal static class PackageStaffEligibility
{
    public static async Task<IReadOnlyList<AvailablePackageStaffDto>> ListAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBranchScope branchScope,
        IIdentityService identity,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");
        var allowedBranches = await branchScope.GetAllowedBranchIdsAsync(userId, cancellationToken);
        var allowedBranchIds = allowedBranches.ToArray();
        var candidates = await db.Employees.AsNoTracking()
            .Where(employee => employee.TenantId == tenantId && employee.IsActive)
            .Where(employee => employee.UserBranches.Any(branch =>
                branch.TenantId == tenantId && allowedBranchIds.Contains(branch.BranchId)))
            .OrderBy(employee => employee.FullName)
            .Select(employee => new { employee.Id, employee.IdentityUserId, employee.FullName })
            .ToListAsync(cancellationToken);
        var roles = await identity.GetRolesAsync(
            candidates.Select(employee => employee.IdentityUserId), cancellationToken);
        return candidates
            .Where(employee => string.Equals(
                roles.GetValueOrDefault(employee.IdentityUserId),
                Roles.Staff,
                StringComparison.OrdinalIgnoreCase))
            .Select(employee => new AvailablePackageStaffDto(employee.Id, employee.FullName))
            .ToArray();
    }

    public static async Task ValidateSelectionAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBranchScope branchScope,
        IIdentityService identity,
        string tenantId,
        bool allStaff,
        IReadOnlyList<string>? employeeIds,
        CancellationToken cancellationToken)
    {
        var ids = employeeIds ?? [];
        if (allStaff && ids.Count == 0)
        {
            return;
        }
        if (allStaff || ids.Count == 0 || ids.Any(string.IsNullOrWhiteSpace) ||
            ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
        {
            throw Invalid();
        }
        var eligible = await ListAsync(db, currentUser, branchScope, identity, tenantId, cancellationToken);
        if (ids.Any(id => eligible.All(employee => employee.Id != id)))
        {
            throw Invalid();
        }
    }

    private static ValidationException Invalid() =>
        new([new ValidationFailure("staffIds", ApiErrorCodes.Packages.PackageStaffInvalid)
        {
            ErrorCode = ApiErrorCodes.Packages.PackageStaffInvalid
        }]);
}

public sealed record AvailablePackageStaffDto(string Id, string FullName);
