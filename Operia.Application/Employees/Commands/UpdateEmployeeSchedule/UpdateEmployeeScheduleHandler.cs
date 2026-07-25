using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Employees.Common;
using Operia.Domain.Entities;

namespace Operia.Application.Employees.Commands.UpdateEmployeeSchedule;

public sealed class UpdateEmployeeScheduleHandler : IRequestHandler<UpdateEmployeeScheduleCommand, EmployeeScheduleDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public UpdateEmployeeScheduleHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<EmployeeScheduleDto> Handle(UpdateEmployeeScheduleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = EmployeeHandlerHelpers.RequireTenant(_currentUserService);
        await EmployeeScheduleHelpers.EnsureEmployeeAsync(_db, tenantId, request.EmployeeId, cancellationToken);
        var businessDays = await EmployeeScheduleHelpers.GetBusinessDaysAsync(_db, tenantId, cancellationToken);
        EmployeeScheduleHelpers.ValidateWithinBusinessHours(request.Days, businessDays);

        var existing = await _db.EmployeeWorkingDays
            .Where(x => x.TenantId == tenantId && x.EmployeeId == request.EmployeeId)
            .ToDictionaryAsync(x => x.Day, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var previous = EmployeeScheduleHelpers.ToWeek(existing.Values);

        foreach (var requestDay in request.Days)
        {
            var day = requestDay.Day.ToLowerInvariant();
            if (!existing.TryGetValue(day, out var entity))
            {
                entity = new EmployeeWorkingDay
                {
                    TenantId = tenantId,
                    EmployeeId = request.EmployeeId,
                    Day = day
                };
                _db.EmployeeWorkingDays.Add(entity);
            }

            entity.Enabled = requestDay.Enabled;
            entity.FromTime = requestDay.Enabled ? requestDay.FromTime : null;
            entity.ToTime = requestDay.Enabled ? requestDay.ToTime : null;
        }

        var current = EmployeeScheduleHelpers.ToWeek(request.Days);
        var emp = await _db.Employees.AsNoTracking().SingleAsync(x => x.Id == request.EmployeeId, cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = _currentUserService.UserId,
            UserDisplayName = _currentUserService.DisplayName,
            Action = "EmployeeScheduleUpdated",
            EntityType = nameof(Employee),
            EntityId = request.EmployeeId,
            EntityName = emp.FullName,
            DetailsJson = JsonSerializer.Serialize(new { previous, current }, JsonOptions)
        });

        await _db.SaveChangesAsync(cancellationToken);
        return new EmployeeScheduleDto(current);
    }
}
