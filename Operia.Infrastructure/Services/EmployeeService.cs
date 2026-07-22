using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.PhoneNumbers;
using Operia.Application.Employees;
using Operia.Domain.Entities;
using Operia.Domain.Exceptions;
using Operia.Infrastructure.Identity;
using Operia.Infrastructure.Persistence;

namespace Operia.Infrastructure.Services;

public sealed class EmployeeService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IFileStorageService files,
    ITokenService tokens) : IEmployeeService
{
    private static readonly string[] AllowedRoles = [Roles.SuperAdmin, Roles.Admin, Roles.Reception, Roles.Staff];

    public async Task<EmployeeListResult> ListAsync(EmployeeListFilter filter, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var page = Math.Max(1, filter.PageNumber);
        var size = Math.Clamp(filter.PageSize, 1, 50);
        var query = db.Employees.AsNoTracking().Where(x => x.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x => x.FullName.Contains(search) || x.Code.Contains(search));
        }
        if (filter.IsActive is not null) query = query.Where(x => x.IsActive == filter.IsActive);
        if (!string.IsNullOrWhiteSpace(filter.BranchId)) query = query.Where(x => x.UserBranches.Any(b => b.BranchId == filter.BranchId));
        if (filter.CreatedFrom is not null) query = query.Where(x => x.CreatedAt >= filter.CreatedFrom.Value.ToDateTime(TimeOnly.MinValue));
        if (filter.CreatedTo is not null) query = query.Where(x => x.CreatedAt < filter.CreatedTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var candidates = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var roles = await LoadRolesAsync(candidates.Select(x => x.IdentityUserId));
        if (!string.IsNullOrWhiteSpace(filter.Role))
            candidates = candidates.Where(x => roles.GetValueOrDefault(x.IdentityUserId) == filter.Role).ToList();
        var counts = AllowedRoles.Select(role => new EmployeeRoleCountDto(role, candidates.Count(x => roles.GetValueOrDefault(x.IdentityUserId) == role))).ToList();
        var total = candidates.Count;
        var selected = candidates.Skip((page - 1) * size).Take(size).ToList();
        var items = await MapManyAsync(selected, roles, ct);
        return new EmployeeListResult(items, page, size, total, (int)Math.Ceiling(total / (double)size), counts);
    }

    public async Task<EmployeeDto> GetAsync(string id, CancellationToken ct)
    {
        var employee = await TenantEmployees().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(nameof(Employee), id);
        return (await MapManyAsync([employee], await LoadRolesAsync([employee.IdentityUserId]), ct))[0];
    }

    public async Task<IReadOnlyList<BookableEmployeeDto>> BookableAsync(string branchId, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var validBranch = await db.Branches.AnyAsync(x => x.Id == branchId && x.TenantId == tenantId, ct);
        if (!validBranch) throw new NotFoundException(nameof(Branch), branchId);
        var now = DateTimeOffset.UtcNow;
        return await (from employee in db.Employees.AsNoTracking()
                      join user in db.Users.AsNoTracking() on employee.IdentityUserId equals user.Id
                      where employee.TenantId == tenantId && employee.IsActive &&
                            (user.LockoutEnd == null || user.LockoutEnd <= now) &&
                            employee.UserBranches.Any(x => x.BranchId == branchId)
                      orderby employee.FullName
                      select new BookableEmployeeDto(employee.Id, employee.Code, employee.FullName, employee.PhotoUrl, employee.Specialty, employee.JobTitle)).ToListAsync(ct);
    }

    public async Task<EmployeeDto> CreateAsync(EmployeeWriteModel model, CancellationToken ct)
    {
        ValidateRole(model.Role);
        await ValidateBranchesAsync(model.BranchIds, ct);
        await EnsureIdentityUniqueAsync(null, model, ct);
        if (string.IsNullOrWhiteSpace(model.TemporaryPassword)) throw Invalid("temporaryPassword", "Temporary password is required.");
        string? newPhoto = null;
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            if (model.Photo is not null) newPhoto = await SavePhotoAsync(model.Photo, ct);
            var code = await NextCodeAsync(ct);
            var user = new ApplicationUser
            {
                FullName = model.FullName.Trim(),
                UserName = model.UserName.Trim(),
                Email = model.Email.Trim(),
                PhoneNumber = PhoneNumberHelper.ToE164(model.MobileNumber),
                TenantId = RequireTenant(),
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                MustChangePassword = true
            };
            EnsureIdentity(await userManager.CreateAsync(user, model.TemporaryPassword), "temporaryPassword");
            EnsureIdentity(await userManager.AddToRoleAsync(user, model.Role), "role");
            if (!model.IsActive)
            {
                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.MaxValue;
                EnsureIdentity(await userManager.UpdateAsync(user), "status");
            }
            var employee = new Employee
            {
                TenantId = RequireTenant(),
                IdentityUserId = user.Id,
                Code = code,
                FullName = user.FullName,
                Email = user.Email!,
                MobileNumber = user.PhoneNumber!,
                Specialty = Clean(model.Specialty),
                JobTitle = Clean(model.JobTitle),
                JoiningDate = model.JoiningDate,
                PhotoUrl = newPhoto,
                IsActive = model.IsActive
            };
            db.Employees.Add(employee);
            AddBranches(employee, model.BranchIds);
            AddAudit("EmployeeCreated", employee, new { employee.Code, role = model.Role, branchIds = model.BranchIds, employee.IsActive });
            await unitOfWork.CommitTransactionAsync(ct);
            return await GetAsync(employee.Id, ct);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            if (newPhoto is not null) await files.DeleteAsync(newPhoto, ct);
            throw;
        }
    }

    public async Task<EmployeeDto> UpdateAsync(string id, EmployeeWriteModel model, CancellationToken ct)
    {
        ValidateRole(model.Role);
        await ValidateBranchesAsync(model.BranchIds, ct);
        var employee = await TenantEmployees().Include(x => x.UserBranches).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(nameof(Employee), id);
        var user = await userManager.FindByIdAsync(employee.IdentityUserId) ?? throw new NotFoundException(nameof(ApplicationUser), employee.IdentityUserId);
        await EnsureIdentityUniqueAsync(user.Id, model, ct);
        var oldPhoto = employee.PhotoUrl;
        string? newPhoto = null;
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            if (model.Photo is not null) newPhoto = await SavePhotoAsync(model.Photo, ct);
            user.FullName = employee.FullName = model.FullName.Trim();
            user.Email = employee.Email = model.Email.Trim();
            user.PhoneNumber = employee.MobileNumber = PhoneNumberHelper.ToE164(model.MobileNumber);
            user.UserName = model.UserName.Trim();
            EnsureIdentity(await userManager.UpdateAsync(user), "identity");
            employee.Specialty = Clean(model.Specialty); employee.JobTitle = Clean(model.JobTitle); employee.JoiningDate = model.JoiningDate;
            if (newPhoto is not null) employee.PhotoUrl = newPhoto; else if (model.RemovePhoto) employee.PhotoUrl = null;
            db.UserBranches.RemoveRange(employee.UserBranches); AddBranches(employee, model.BranchIds);
            var oldRole = (await userManager.GetRolesAsync(user)).Single();
            if (oldRole != model.Role)
            {
                await ProtectLastSuperAdminAsync(employee, oldRole, false, ct);
                EnsureIdentity(await userManager.RemoveFromRoleAsync(user, oldRole), "role");
                EnsureIdentity(await userManager.AddToRoleAsync(user, model.Role), "role");
                AddAudit("RoleChanged", employee, new { oldRole, newRole = model.Role });
            }
            if (employee.IsActive != model.IsActive)
            {
                if (!model.IsActive && employee.IdentityUserId == currentUser.UserId)
                    throw new ConflictException("You cannot deactivate your own employee account.");
                await ProtectLastSuperAdminAsync(employee, model.Role, !model.IsActive, ct);
                var oldStatus = employee.IsActive;
                employee.IsActive = model.IsActive;
                user.LockoutEnabled = true;
                user.LockoutEnd = model.IsActive ? null : DateTimeOffset.MaxValue;
                EnsureIdentity(await userManager.UpdateSecurityStampAsync(user), "status");
                EnsureIdentity(await userManager.UpdateAsync(user), "status");
                await tokens.RevokeAllRefreshTokensAsync(user.Id, ct);
                AddAudit("StatusChanged", employee, new { oldStatus, newStatus = model.IsActive });
            }
            AddAudit("EmployeeUpdated", employee, new { branchIds = model.BranchIds, photoChanged = newPhoto is not null || model.RemovePhoto });
            await unitOfWork.CommitTransactionAsync(ct);
            if (oldPhoto is not null && oldPhoto != employee.PhotoUrl) await files.DeleteAsync(oldPhoto, ct);
            return await GetAsync(id, ct);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(ct);
            if (newPhoto is not null) await files.DeleteAsync(newPhoto, ct);
            throw;
        }
    }

    public async Task ChangeRoleAsync(string id, string role, CancellationToken ct)
    {
        ValidateRole(role);
        var employee = await TenantEmployees().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException(nameof(Employee), id);
        var user = await userManager.FindByIdAsync(employee.IdentityUserId) ?? throw new NotFoundException(nameof(ApplicationUser), employee.IdentityUserId);
        var oldRole = (await userManager.GetRolesAsync(user)).Single();
        if (oldRole == role) return;
        await ProtectLastSuperAdminAsync(employee, oldRole, false, ct);
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            EnsureIdentity(await userManager.RemoveFromRoleAsync(user, oldRole), "role");
            EnsureIdentity(await userManager.AddToRoleAsync(user, role), "role");
            AddAudit("RoleChanged", employee, new { oldRole, newRole = role });
            await unitOfWork.CommitTransactionAsync(ct);
        }
        catch { await unitOfWork.RollbackTransactionAsync(ct); throw; }
    }

    public async Task ChangeStatusAsync(string id, bool isActive, CancellationToken ct)
    {
        var employee = await TenantEmployees().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException(nameof(Employee), id);
        if (employee.IsActive == isActive) return;
        if (!isActive && employee.IdentityUserId == currentUser.UserId) throw new ConflictException("You cannot deactivate your own employee account.");
        var user = await userManager.FindByIdAsync(employee.IdentityUserId) ?? throw new NotFoundException(nameof(ApplicationUser), employee.IdentityUserId);
        var role = (await userManager.GetRolesAsync(user)).Single();
        await ProtectLastSuperAdminAsync(employee, role, !isActive, ct);
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var oldStatus = employee.IsActive;
            employee.IsActive = isActive;
            user.LockoutEnabled = true;
            user.LockoutEnd = isActive ? null : DateTimeOffset.MaxValue;
            EnsureIdentity(await userManager.UpdateSecurityStampAsync(user), "status");
            EnsureIdentity(await userManager.UpdateAsync(user), "status");
            await tokens.RevokeAllRefreshTokensAsync(user.Id, ct);
            AddAudit("StatusChanged", employee, new { oldStatus, newStatus = isActive });
            await unitOfWork.CommitTransactionAsync(ct);
        }
        catch { await unitOfWork.RollbackTransactionAsync(ct); throw; }
    }

    private IQueryable<Employee> TenantEmployees() => db.Employees.Where(x => x.TenantId == RequireTenant());
    private string RequireTenant() => currentUser.TenantId ?? throw new UnauthorizedAccessException("The current user does not have a tenant.");
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void ValidateRole(string role) { if (!AllowedRoles.Contains(role)) throw Invalid("role", "Invalid employee role."); }
    private static ValidationException Invalid(string field, string message) => new([new FluentValidation.Results.ValidationFailure(field, message)]);
    private static void EnsureIdentity(IdentityResult result, string field) { if (!result.Succeeded) throw new ValidationException(result.Errors.Select(x => new FluentValidation.Results.ValidationFailure(field, x.Description))); }

    private async Task ValidateBranchesAsync(IReadOnlyList<string> branchIds, CancellationToken ct)
    {
        var ids = branchIds.Distinct().ToList();
        if (ids.Count == 0) throw Invalid("branchIds", "At least one branch is required.");
        if (await db.Branches.CountAsync(x => x.TenantId == RequireTenant() && ids.Contains(x.Id), ct) != ids.Count)
            throw Invalid("branchIds", "One or more branches are invalid for this tenant.");
    }

    private async Task EnsureIdentityUniqueAsync(string? userId, EmployeeWriteModel model, CancellationToken ct)
    {
        var normalizedName = userManager.NormalizeName(model.UserName.Trim());
        var normalizedEmail = userManager.NormalizeEmail(model.Email.Trim());
        var mobile = PhoneNumberHelper.ToE164(model.MobileNumber);
        if (await userManager.Users.AnyAsync(x => x.Id != userId && (x.NormalizedUserName == normalizedName || x.NormalizedEmail == normalizedEmail || x.PhoneNumber == mobile), ct))
            throw new ConflictException("Username, email, or mobile number is already registered.");
    }

    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            IF NOT EXISTS (SELECT 1 FROM TenantNumberCounters WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @tenantId)
                INSERT INTO TenantNumberCounters (TenantId, LastEmployeeNumber, CreatedAt) VALUES (@tenantId, 0, SYSUTCDATETIME());
            UPDATE TenantNumberCounters WITH (UPDLOCK, ROWLOCK)
            SET LastEmployeeNumber = LastEmployeeNumber + 1, LastModifiedAt = SYSUTCDATETIME()
            OUTPUT INSERTED.LastEmployeeNumber
            WHERE TenantId = @tenantId AND LastEmployeeNumber < 9999;
            """;
        var parameter = command.CreateParameter(); parameter.ParameterName = "@tenantId"; parameter.Value = RequireTenant(); command.Parameters.Add(parameter);
        var value = await command.ExecuteScalarAsync(ct);
        if (value is null or DBNull) throw new ConflictException("Employee code range EMP-0001 through EMP-9999 is exhausted.");
        return $"EMP-{Convert.ToInt32(value):0000}";
    }

    private void AddBranches(Employee employee, IEnumerable<string> ids)
    {
        foreach (var branchId in ids.Distinct()) employee.UserBranches.Add(new UserBranch { TenantId = employee.TenantId, EmployeeId = employee.Id, BranchId = branchId });
    }

    private void AddAudit(string action, Employee employee, object details) => db.AuditLogs.Add(new AuditLog
    {
        TenantId = RequireTenant(),
        UserId = currentUser.UserId,
        UserDisplayName = currentUser.DisplayName,
        Action = action,
        EntityType = nameof(Employee),
        EntityId = employee.Id,
        EntityName = employee.FullName,
        DetailsJson = JsonSerializer.Serialize(details)
    });

    private async Task<string> SavePhotoAsync(Application.Common.Models.FileUploadContent photo, CancellationToken ct)
        => await files.SaveAsync(photo.Content, photo.FileName, photo.ContentType, RequireTenant(), FileUploadCategory.EmployeePhoto, ct);

    private async Task<Dictionary<string, string>> LoadRolesAsync(IEnumerable<string> ids)
    {
        var result = new Dictionary<string, string>();
        foreach (var id in ids.Distinct())
        {
            var user = await userManager.FindByIdAsync(id);
            if (user is not null) result[id] = (await userManager.GetRolesAsync(user)).SingleOrDefault() ?? string.Empty;
        }
        return result;
    }

    private async Task<IReadOnlyList<EmployeeDto>> MapManyAsync(IReadOnlyList<Employee> employees, Dictionary<string, string> roles, CancellationToken ct)
    {
        var ids = employees.Select(x => x.Id).ToList();
        var branches = await db.UserBranches.AsNoTracking().Where(x => ids.Contains(x.EmployeeId))
            .Join(db.Branches.AsNoTracking(), x => x.BranchId, x => x.Id, (map, branch) => new { map.EmployeeId, Branch = new EmployeeBranchDto(branch.Id, branch.Name) }).ToListAsync(ct);
        var users = await userManager.Users.AsNoTracking().Where(x => employees.Select(e => e.IdentityUserId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return employees.Select(x => new EmployeeDto(x.Id, x.Code, x.FullName, x.Email, x.MobileNumber,
            users.GetValueOrDefault(x.IdentityUserId)?.UserName ?? string.Empty, x.Specialty, x.JobTitle, x.JoiningDate,
            x.PhotoUrl, x.IsActive, roles.GetValueOrDefault(x.IdentityUserId) ?? string.Empty,
            branches.Where(b => b.EmployeeId == x.Id).Select(b => b.Branch).ToList(), x.CreatedAt)).ToList();
    }

    private async Task ProtectLastSuperAdminAsync(Employee employee, string currentRole, bool deactivating, CancellationToken ct)
    {
        if (currentRole != Roles.SuperAdmin || (!deactivating && employee.IsActive == false)) return;
        var activeIds = await TenantEmployees().Where(x => x.IsActive && x.Id != employee.Id).Select(x => x.IdentityUserId).ToListAsync(ct);
        foreach (var id in activeIds)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user is not null && await userManager.IsInRoleAsync(user, Roles.SuperAdmin)) return;
        }
        throw new ConflictException("The tenant must retain at least one active Super Admin.");
    }
}
