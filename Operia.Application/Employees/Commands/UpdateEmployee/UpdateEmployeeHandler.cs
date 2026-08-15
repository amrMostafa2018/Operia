using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.PhoneNumbers;
using Operia.Application.Employees.Common;
using Operia.Domain.Entities;
using Operia.Domain.Exceptions;

namespace Operia.Application.Employees.Commands.UpdateEmployee;

public sealed class UpdateEmployeeHandler : IRequestHandler<UpdateEmployeeCommand, EmployeeDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _files;
    private readonly IAuditWriter _auditWriter;

    public UpdateEmployeeHandler(
        IApplicationDbContext db,
        IIdentityService identityService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IFileStorageService files,
        IAuditWriter auditWriter)
    {
        _db = db;
        _identityService = identityService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _files = files;
        _auditWriter = auditWriter;
    }

    public async Task<EmployeeDto> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var tenantId = EmployeeHandlerHelpers.RequireTenant(_currentUserService);
        EmployeeHandlerHelpers.ValidateRole(request.Role);

        var employee = await _db.Employees.Include(x => x.UserBranches)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.Id);

        await EmployeeHandlerHelpers.ValidateBranchesAsync(_db, employee.BusinessId, request.BranchIds, cancellationToken);
        await EmployeeIdentityUniquenessHelper.EnsureUniqueAsync(
            _identityService,
            employee.IdentityUserId,
            request.UserName,
            request.Email,
            request.MobileNumber,
            cancellationToken);

        var oldPhoto = employee.PhotoUrl;
        string? newPhoto = null;
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (request.Photo is not null)
                newPhoto = await EmployeeHandlerHelpers.SavePhotoAsync(_files, tenantId, request.Photo, cancellationToken);

            await _identityService.UpdateUserAsync(
                employee.IdentityUserId,
                request.FullName,
                request.UserName,
                request.Email,
                request.MobileNumber,
                cancellationToken);

            employee.FullName = request.FullName.Trim();
            employee.Email = request.Email.Trim();
            employee.MobileNumber = PhoneNumberHelper.ToE164(request.MobileNumber);
            employee.Specialty = EmployeeHandlerHelpers.Clean(request.Specialty);
            employee.JobTitle = EmployeeHandlerHelpers.Clean(request.JobTitle);
            employee.JoiningDate = request.JoiningDate;

            if (newPhoto is not null)
                employee.PhotoUrl = newPhoto;
            else if (request.RemovePhoto)
                employee.PhotoUrl = null;

            var oldBranchIds = employee.UserBranches.Select(x => x.BranchId).Distinct().ToList();
            var newBranchIds = request.BranchIds.Distinct().ToList();
            _db.UserBranches.RemoveRange(employee.UserBranches);
            EmployeeHandlerHelpers.AddBranches(employee, newBranchIds);

            var rolesMap = await _identityService.GetRolesAsync([employee.IdentityUserId], cancellationToken);
            var oldRole = rolesMap.GetValueOrDefault(employee.IdentityUserId) ?? string.Empty;

            if (!string.Equals(oldRole, request.Role, StringComparison.OrdinalIgnoreCase))
            {
                await EmployeeHandlerHelpers.ProtectLastSuperAdminAsync(_db, _identityService, tenantId, employee, oldRole, false, cancellationToken);
                await _identityService.ChangeRoleAsync(employee.IdentityUserId, request.Role, cancellationToken);
                EmployeeHandlerHelpers.AddAudit(_auditWriter, tenantId, AuditActions.EmployeeRoleChanged, employee, new { oldRole, newRole = request.Role });
            }

            if (employee.IsActive != request.IsActive)
            {
                if (!request.IsActive && employee.IdentityUserId == _currentUserService.UserId)
                    throw new ConflictException("You cannot deactivate your own employee account.");

                await EmployeeHandlerHelpers.ProtectLastSuperAdminAsync(_db, _identityService, tenantId, employee, request.Role, !request.IsActive, cancellationToken);
                var oldStatus = employee.IsActive;
                employee.IsActive = request.IsActive;
                await _identityService.SetStatusAsync(employee.IdentityUserId, request.IsActive, cancellationToken);
                EmployeeHandlerHelpers.AddAudit(_auditWriter, tenantId, AuditActions.EmployeeStatusChanged, employee, new { oldStatus, newStatus = request.IsActive });
            }

            EmployeeHandlerHelpers.AddAudit(_auditWriter, tenantId, AuditActions.EmployeeUpdated, employee, new { oldBranchIds, newBranchIds, branchIds = newBranchIds, photoChanged = newPhoto is not null || request.RemovePhoto });
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            if (oldPhoto is not null && oldPhoto != employee.PhotoUrl)
                await _files.DeleteAsync(oldPhoto, cancellationToken);

            var updatedRoles = await _identityService.GetRolesAsync([employee.IdentityUserId], cancellationToken);
            var list = await EmployeeHandlerHelpers.MapManyAsync(_db, _identityService, [employee], updatedRoles, cancellationToken);
            return list.Single();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            if (newPhoto is not null)
                await _files.DeleteAsync(newPhoto, cancellationToken);
            throw;
        }
    }
}
