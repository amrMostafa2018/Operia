using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Employees.Common;
using Operia.Domain.Entities;
using Operia.SharedKernel.Errors;
using ValidationException = Operia.Application.Common.Exceptions.ValidationException;

namespace Operia.Application.Employees.Commands.UpdateEmployeeSchedule;

public sealed class UpdateEmployeeScheduleHandler : IRequestHandler<UpdateEmployeeScheduleCommand, EmployeeScheduleDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditWriter _auditWriter;

    public UpdateEmployeeScheduleHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUserService,
        IAuditWriter auditWriter)
    {
        _db = db;
        _currentUserService = currentUserService;
        _auditWriter = auditWriter;
    }

    public async Task<EmployeeScheduleDto> Handle(UpdateEmployeeScheduleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = EmployeeHandlerHelpers.RequireTenant(_currentUserService);
        await EmployeeScheduleHelpers.EnsureEmployeeAsync(_db, tenantId, request.EmployeeId, cancellationToken);

        var assignedBranches = await _db.UserBranches.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.EmployeeId == request.EmployeeId)
            .Select(x => new AssignedBranch(x.BranchId, x.Branch!.Name))
            .ToListAsync(cancellationToken);
        var assignedBranchIds = assignedBranches.Select(x => x.BranchId).ToHashSet(StringComparer.Ordinal);

        var assignmentFailures = request.Branches
            .Select((branch, index) => new { branch, index })
            .Where(x => !assignedBranchIds.Contains(x.branch.BranchId))
            .Select(x => ValidationFailureFactory.Create(
                $"branches[{x.index}].branchId",
                ApiErrorCodes.EmployeeSchedule.BranchNotAssigned))
            .ToList();

        if (assignmentFailures.Count > 0)
            throw new ValidationException(assignmentFailures);

        var businessDays = await EmployeeScheduleHelpers.GetBusinessDaysAsync(_db, tenantId, cancellationToken);
        for (var index = 0; index < request.Branches.Count; index++)
        {
            EmployeeScheduleHelpers.ValidateWithinBusinessHours(
                request.Branches[index].Days,
                businessDays,
                $"branches[{index}].days");
        }

        EmployeeScheduleHelpers.ValidateNoOverlappingBranchSchedules(request.Branches);

        var existing = await _db.EmployeeWorkingDays
            .Where(x => x.TenantId == tenantId && x.EmployeeId == request.EmployeeId)
            .ToListAsync(cancellationToken);
        var existingByKey = existing.ToDictionary(
            x => (x.BranchId, x.Day.ToLowerInvariant()),
            x => x);

        var requestedBranchIds = request.Branches.Select(x => x.BranchId).ToHashSet(StringComparer.Ordinal);
        foreach (var orphan in existing.Where(x => !requestedBranchIds.Contains(x.BranchId)).ToList())
            _db.EmployeeWorkingDays.Remove(orphan);

        var previous = BuildAuditSnapshot(existing, assignedBranches);
        var currentBranches = new List<EmployeeBranchScheduleDto>();

        foreach (var branchSchedule in request.Branches)
        {
            var week = EmployeeScheduleHelpers.ToWeek(branchSchedule.Days);
            foreach (var requestDay in week)
            {
                var day = requestDay.Day.ToLowerInvariant();
                var key = (branchSchedule.BranchId, day);
                if (!existingByKey.TryGetValue(key, out var entity))
                {
                    entity = new EmployeeWorkingDay
                    {
                        TenantId = tenantId,
                        EmployeeId = request.EmployeeId,
                        BranchId = branchSchedule.BranchId,
                        Day = day
                    };
                    _db.EmployeeWorkingDays.Add(entity);
                    existingByKey[key] = entity;
                }

                entity.Enabled = requestDay.Enabled;
                entity.FromTime = requestDay.Enabled ? requestDay.FromTime : null;
                entity.ToTime = requestDay.Enabled ? requestDay.ToTime : null;
            }

            var branchName = assignedBranches.Single(x => x.BranchId == branchSchedule.BranchId).BranchName;
            currentBranches.Add(new EmployeeBranchScheduleDto(branchSchedule.BranchId, branchName, week));
        }

        var emp = await _db.Employees.AsNoTracking().SingleAsync(x => x.Id == request.EmployeeId, cancellationToken);
        _auditWriter.Write(
            tenantId,
            AuditActions.EmployeeScheduleUpdated,
            nameof(Employee),
            request.EmployeeId,
            emp.FullName,
            new { previous, current = currentBranches });

        await _db.SaveChangesAsync(cancellationToken);
        return new EmployeeScheduleDto(currentBranches);
    }

    private static IReadOnlyList<EmployeeBranchScheduleDto> BuildAuditSnapshot(
        IEnumerable<EmployeeWorkingDay> rows,
        IReadOnlyList<AssignedBranch> assignedBranches)
    {
        var branchNames = assignedBranches.ToDictionary(x => x.BranchId, x => x.BranchName, StringComparer.Ordinal);
        return rows
            .GroupBy(x => x.BranchId)
            .Select(group => new EmployeeBranchScheduleDto(
                group.Key,
                branchNames.TryGetValue(group.Key, out var name) ? name : group.Key,
                EmployeeScheduleHelpers.ToWeek(group)))
            .ToList();
    }

    private sealed record AssignedBranch(string BranchId, string BranchName);
}
