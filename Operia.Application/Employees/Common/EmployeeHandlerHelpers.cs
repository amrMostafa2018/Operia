using System.Text.Json;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.Models;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Exceptions;
using ValidationException = Operia.Application.Common.Exceptions.ValidationException;

namespace Operia.Application.Employees.Common;

internal static class EmployeeHandlerHelpers
{
    private static readonly string[] AllowedRoles = [Roles.SuperAdmin, Roles.Admin, Roles.Reception, Roles.Staff];

    public static string RequireTenant(ICurrentUserService currentUser) =>
        currentUser.TenantId ?? throw new UnauthorizedAccessException("The current user does not have a tenant.");

    public static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static void ValidateRole(string role)
    {
        if (!AllowedRoles.Contains(role))
            throw Invalid("role", "Invalid employee role.");
    }

    public static ValidationException Invalid(string field, string message) =>
        new([new ValidationFailure(field, message)]);

    public static async Task ValidateBranchesAsync(
        IApplicationDbContext db,
        string tenantId,
        IReadOnlyList<string> branchIds,
        CancellationToken cancellationToken)
    {
        var ids = branchIds.Distinct().ToList();
        if (ids.Count == 0)
            throw Invalid("branchIds", "At least one branch is required.");
        if (await db.Branches.CountAsync(x => x.TenantId == tenantId && ids.Contains(x.Id), cancellationToken) != ids.Count)
            throw Invalid("branchIds", "One or more branches are invalid for this tenant.");
    }

    public static Task<string> NextCodeAsync(
        IApplicationDbContext db,
        string tenantId,
        CancellationToken cancellationToken) => db.GetNextEmployeeCodeAsync(tenantId, cancellationToken);

    public static void AddBranches(Employee employee, IEnumerable<string> ids)
    {
        foreach (var branchId in ids.Distinct())
            employee.UserBranches.Add(new UserBranch { TenantId = employee.TenantId, EmployeeId = employee.Id, BranchId = branchId });
    }

    public static void AddAudit(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        string tenantId,
        string action,
        Employee employee,
        object details) => db.AuditLogs.Add(new AuditLog
    {
        TenantId = tenantId,
        UserId = currentUser.UserId,
        UserDisplayName = currentUser.DisplayName,
        Action = action,
        EntityType = nameof(Employee),
        EntityId = employee.Id,
        EntityName = employee.FullName,
        DetailsJson = JsonSerializer.Serialize(details)
    });

    public static async Task<string> SavePhotoAsync(
        IFileStorageService files,
        string tenantId,
        FileUploadContent photo,
        CancellationToken cancellationToken)
        => await files.SaveAsync(photo.Content, photo.FileName, photo.ContentType, tenantId, FileUploadCategory.EmployeePhoto, cancellationToken);

    public static async Task<IReadOnlyList<EmployeeDto>> MapManyAsync(
        IApplicationDbContext db,
        IIdentityService identityService,
        IReadOnlyList<Employee> employees,
        Dictionary<string, string> roles,
        CancellationToken cancellationToken)
    {
        var ids = employees.Select(x => x.Id).ToList();
        var branches = await db.UserBranches.AsNoTracking().Where(x => ids.Contains(x.EmployeeId))
            .Join(db.Branches.AsNoTracking(), x => x.BranchId, x => x.Id, (map, branch) => new { map.EmployeeId, Branch = new EmployeeBranchDto(branch.Id, branch.Name) }).ToListAsync(cancellationToken);
        var userIds = employees.Select(e => e.IdentityUserId).Distinct().ToList();
        var users = await identityService.GetUsersSummaryAsync(userIds, cancellationToken);
        return employees.Select(x => new EmployeeDto(x.Id, x.Code, x.FullName, x.Email, x.MobileNumber,
            users.GetValueOrDefault(x.IdentityUserId)?.UserName ?? string.Empty, x.Specialty, x.JobTitle, x.JoiningDate,
            x.PhotoUrl, x.IsActive, roles.GetValueOrDefault(x.IdentityUserId) ?? string.Empty,
            branches.Where(b => b.EmployeeId == x.Id).Select(b => b.Branch).ToList(), x.CreatedAt)).ToList();
    }

    public static async Task ProtectLastSuperAdminAsync(
        IApplicationDbContext db,
        IIdentityService identityService,
        string tenantId,
        Employee employee,
        string currentRole,
        bool deactivating,
        CancellationToken cancellationToken)
    {
        if (currentRole != Roles.SuperAdmin || (!deactivating && employee.IsActive == false))
            return;
        var activeIds = await db.Employees.Where(x => x.TenantId == tenantId && x.IsActive && x.Id != employee.Id).Select(x => x.IdentityUserId).ToListAsync(cancellationToken);
        await identityService.EnsureRetainSuperAdminAsync(activeIds, currentRole, deactivating, cancellationToken);
    }
}
