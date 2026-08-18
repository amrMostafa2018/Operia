using FluentValidation.Results;
using MediatR;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.PhoneNumbers;
using Operia.Application.Employees.Commands.UpdateEmployeeSchedule;
using Operia.Application.Employees.Common;
using Operia.Domain.Entities;
using Operia.SharedKernel.Errors;
using ValidationException = Operia.Application.Common.Exceptions.ValidationException;

namespace Operia.Application.Employees.Commands.CreateEmployee;

public sealed class CreateEmployeeHandler : IRequestHandler<CreateEmployeeCommand, EmployeeDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IEmployeeCodeGenerator _employeeCodeGenerator;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _files;
    private readonly IAuditWriter _auditWriter;

    public CreateEmployeeHandler(
        IApplicationDbContext db,
        IEmployeeCodeGenerator employeeCodeGenerator,
        IIdentityService identityService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IFileStorageService files,
        IAuditWriter auditWriter)
    {
        _db = db;
        _employeeCodeGenerator = employeeCodeGenerator;
        _identityService = identityService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _files = files;
        _auditWriter = auditWriter;
    }

    public async Task<EmployeeDto> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var tenantId = EmployeeHandlerHelpers.RequireTenant(_currentUserService);
        EmployeeHandlerHelpers.ValidateRole(request.Role);
        var business = await EmployeeHandlerHelpers.ValidateBranchesForTenantAsync(
            _db,
            tenantId,
            request.BranchIds,
            cancellationToken);
        await EmployeeIdentityUniquenessHelper.EnsureUniqueAsync(
            _identityService,
            null,
            request.UserName,
            request.Email,
            request.MobileNumber,
            cancellationToken);

        // Validate schedule shape and business-hours containment before the transaction so that
        // schedule errors are surfaced atomically without creating a partial employee record.
        if (request.Schedule is { Count: > 0 })
        {
            var assignedBranchIds = request.BranchIds.ToHashSet(StringComparer.Ordinal);
            var assignmentFailures = request.Schedule
                .Select((branch, index) => new { branch, index })
                .Where(x => !assignedBranchIds.Contains(x.branch.BranchId))
                .Select(x => ValidationFailureFactory.Create(
                    $"branches[{x.index}].branchId",
                    ApiErrorCodes.EmployeeSchedule.BranchNotAssigned))
                .ToList();

            if (assignmentFailures.Count > 0)
                throw new ValidationException(assignmentFailures);

            var businessDays = await EmployeeScheduleHelpers.GetBusinessDaysAsync(_db, tenantId, cancellationToken);
            for (var index = 0; index < request.Schedule.Count; index++)
            {
                EmployeeScheduleHelpers.ValidateWithinBusinessHours(
                    request.Schedule[index].Days,
                    businessDays,
                    $"branches[{index}].days");
            }

            EmployeeScheduleHelpers.ValidateNoOverlappingBranchSchedules(request.Schedule);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        string? photoUrl = null;
        try
        {
            if (request.Photo is not null)
                photoUrl = await EmployeeHandlerHelpers.SavePhotoAsync(_files, tenantId, request.Photo, cancellationToken);

            var identityUserId = await _identityService.CreateUserAsync(
                request.FullName,
                request.UserName,
                request.Email,
                request.MobileNumber,
                request.Role,
                request.TemporaryPassword,
                tenantId,
                cancellationToken);

            var code = await _employeeCodeGenerator.ReserveNextAsync(tenantId, cancellationToken);
            var employee = new Employee
            {
                TenantId = tenantId,
                BusinessId = business.Id,
                IdentityUserId = identityUserId,
                Code = code,
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim(),
                MobileNumber = PhoneNumberHelper.ToE164(request.MobileNumber),
                Specialty = EmployeeHandlerHelpers.Clean(request.Specialty),
                JobTitle = EmployeeHandlerHelpers.Clean(request.JobTitle),
                JoiningDate = request.JoiningDate,
                PhotoUrl = photoUrl,
                IsActive = request.IsActive
            };

            EmployeeHandlerHelpers.AddBranches(employee, request.BranchIds);
            _db.Employees.Add(employee);

            // Persist the initial schedule within the same transaction so employee + schedule
            // are created atomically. Employee.Id is a pre-generated GUID so it is available
            // before SaveChanges.
            if (request.Schedule is { Count: > 0 })
            {
                foreach (var branchSchedule in request.Schedule)
                {
                    var week = EmployeeScheduleHelpers.ToWeek(branchSchedule.Days);
                    foreach (var day in week)
                    {
                        _db.EmployeeWorkingDays.Add(new EmployeeWorkingDay
                        {
                            TenantId = tenantId,
                            EmployeeId = employee.Id,
                            BranchId = branchSchedule.BranchId,
                            Day = day.Day.ToLowerInvariant(),
                            Enabled = day.Enabled,
                            FromTime = day.Enabled ? day.FromTime : null,
                            ToTime = day.Enabled ? day.ToTime : null,
                        });
                    }
                }
            }

            if (!request.IsActive)
            {
                await _identityService.SetStatusAsync(identityUserId, false, cancellationToken);
            }

            EmployeeHandlerHelpers.AddAudit(
                _auditWriter,
                tenantId,
                AuditActions.EmployeeCreated,
                employee,
                new { employee.Code, role = request.Role, branchIds = request.BranchIds.Distinct().ToList() });

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            var roles = new Dictionary<string, string> { [identityUserId] = request.Role };
            var list = await EmployeeHandlerHelpers.MapManyAsync(_db, _identityService, [employee], roles, cancellationToken);
            return list.Single();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            if (photoUrl is not null)
                await _files.DeleteAsync(photoUrl, cancellationToken);
            throw;
        }
    }
}
