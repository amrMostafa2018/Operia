using System.Text.Json;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings;
using Operia.Domain.Entities;
using Operia.Domain.Exceptions;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Employees;

public sealed class EmployeeScheduleService(
    IApplicationDbContext db,
    ICurrentUserService currentUser) : IEmployeeScheduleService
{
    private static readonly string[] WeekDays = ["fri", "sat", "sun", "mon", "tue", "wed", "thu"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EmployeeScheduleDto> GetAsync(string employeeId, CancellationToken cancellationToken)
    {
        await EnsureEmployeeAsync(employeeId, cancellationToken);
        var days = await db.EmployeeWorkingDays.AsNoTracking()
            .Where(x => x.TenantId == RequireTenant() && x.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);

        return new EmployeeScheduleDto(ToWeek(days));
    }

    public async Task<EmployeeScheduleDto> UpdateAsync(
        string employeeId,
        IReadOnlyList<EmployeeWorkingDayDto> requestDays,
        CancellationToken cancellationToken)
    {
        await EnsureEmployeeAsync(employeeId, cancellationToken);
        var tenantId = RequireTenant();
        var businessDays = await GetBusinessDaysAsync(cancellationToken);
        ValidateWithinBusinessHours(requestDays, businessDays);

        var existing = await db.EmployeeWorkingDays
            .Where(x => x.TenantId == tenantId && x.EmployeeId == employeeId)
            .ToDictionaryAsync(x => x.Day, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var previous = ToWeek(existing.Values);

        foreach (var requestDay in requestDays)
        {
            var day = requestDay.Day.ToLowerInvariant();
            if (!existing.TryGetValue(day, out var entity))
            {
                entity = new EmployeeWorkingDay
                {
                    TenantId = tenantId,
                    EmployeeId = employeeId,
                    Day = day
                };
                db.EmployeeWorkingDays.Add(entity);
            }

            entity.Enabled = requestDay.Enabled;
            entity.FromTime = requestDay.Enabled ? requestDay.FromTime : null;
            entity.ToTime = requestDay.Enabled ? requestDay.ToTime : null;
        }

        var current = ToWeek(requestDays);
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = currentUser.UserId,
            UserDisplayName = currentUser.DisplayName,
            Action = "EmployeeScheduleUpdated",
            EntityType = nameof(Employee),
            EntityId = employeeId,
            EntityName = (await db.Employees.AsNoTracking().SingleAsync(x => x.Id == employeeId, cancellationToken)).FullName,
            DetailsJson = JsonSerializer.Serialize(new { previous, current }, JsonOptions)
        });

        await db.SaveChangesAsync(cancellationToken);
        return new EmployeeScheduleDto(current);
    }

    public async Task<EmployeeWorkingDayDto?> GetWorkingDayAsync(string employeeId, string day, CancellationToken cancellationToken)
    {
        await EnsureEmployeeAsync(employeeId, cancellationToken);
        var normalizedDay = day.ToLowerInvariant();
        var workingDay = await db.EmployeeWorkingDays.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == RequireTenant() && x.EmployeeId == employeeId && x.Day == normalizedDay, cancellationToken);

        return workingDay is null
            ? null
            : new EmployeeWorkingDayDto(workingDay.Day, workingDay.Enabled, workingDay.FromTime, workingDay.ToTime);
    }

    private async Task<Employee> EnsureEmployeeAsync(string employeeId, CancellationToken cancellationToken)
        => await db.Employees.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == employeeId && x.TenantId == RequireTenant(), cancellationToken)
           ?? throw new NotFoundException(nameof(Employee), employeeId);

    private async Task<IReadOnlyList<WorkingDayDto>> GetBusinessDaysAsync(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var business = await db.Businesses.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Business), tenantId);
        var settings = await db.BusinessSettings.AsNoTracking().SingleOrDefaultAsync(x => x.BusinessId == business.Id, cancellationToken);
        if (settings is null || string.IsNullOrWhiteSpace(settings.WorkingDaysJson))
            return WorkingDayDto.DefaultWeek;

        try
        {
            var days = JsonSerializer.Deserialize<IReadOnlyList<WorkingDayDto>>(settings.WorkingDaysJson, JsonOptions);
            return IsCompleteWeek(days) ? days! : WorkingDayDto.DefaultWeek;
        }
        catch (JsonException)
        {
            return WorkingDayDto.DefaultWeek;
        }
    }

    private static void ValidateWithinBusinessHours(
        IReadOnlyList<EmployeeWorkingDayDto> employeeDays,
        IReadOnlyList<WorkingDayDto> businessDays)
    {
        var failures = employeeDays
            .Select((employeeDay, index) => new { employeeDay, index, businessDay = businessDays.Single(x => x.Day.Equals(employeeDay.Day, StringComparison.OrdinalIgnoreCase)) })
            .Where(x => x.employeeDay.Enabled &&
                        (!x.businessDay.Enabled ||
                         x.employeeDay.FromTime < x.businessDay.FromTime ||
                         x.employeeDay.ToTime > x.businessDay.ToTime))
            .Select(x => new ValidationFailure($"days[{x.index}]", "Employee working hours must be within business working hours.")
            {
                ErrorCode = ApiErrorCodes.EmployeeSchedule.OutsideBusinessHours
            })
            .ToList();

        if (failures.Count > 0)
            throw new UnprocessableEntityException(failures);
    }

    private static IReadOnlyList<EmployeeWorkingDayDto> ToWeek(IEnumerable<EmployeeWorkingDay> days)
    {
        var byDay = days.ToDictionary(x => x.Day, StringComparer.OrdinalIgnoreCase);
        return WeekDays.Select(day => byDay.TryGetValue(day, out var value)
            ? new EmployeeWorkingDayDto(day, value.Enabled, value.Enabled ? value.FromTime : null, value.Enabled ? value.ToTime : null)
            : new EmployeeWorkingDayDto(day, false, null, null)).ToList();
    }

    private static IReadOnlyList<EmployeeWorkingDayDto> ToWeek(IEnumerable<EmployeeWorkingDayDto> days)
    {
        var byDay = days.ToDictionary(x => x.Day, StringComparer.OrdinalIgnoreCase);
        return WeekDays.Select(day => byDay[day]).ToList();
    }

    private static bool IsCompleteWeek(IReadOnlyList<WorkingDayDto>? days)
        => days is not null && days.Count == WeekDays.Length &&
           days.Select(x => x.Day).Distinct(StringComparer.OrdinalIgnoreCase).Count() == WeekDays.Length &&
           days.All(x => WeekDays.Contains(x.Day, StringComparer.OrdinalIgnoreCase));

    private string RequireTenant() => currentUser.TenantId ?? throw new UnauthorizedAccessException("The current user does not have a tenant.");
}
