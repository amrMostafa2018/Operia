using System.Text.Json;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings;
using Operia.Domain.Entities;
using Operia.Domain.Exceptions;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Employees.Common;

internal static class EmployeeScheduleHelpers
{
    private static readonly string[] WeekDays = ["fri", "sat", "sun", "mon", "tue", "wed", "thu"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<Employee> EnsureEmployeeAsync(
        IApplicationDbContext db,
        string tenantId,
        string employeeId,
        CancellationToken cancellationToken)
        => await db.Employees.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == employeeId && x.TenantId == tenantId, cancellationToken)
           ?? throw new NotFoundException(nameof(Employee), employeeId);

    public static async Task<IReadOnlyList<WorkingDayDto>> GetBusinessDaysAsync(
        IApplicationDbContext db,
        string tenantId,
        CancellationToken cancellationToken)
    {
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

    public static void ValidateWithinBusinessHours(
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

    public static IReadOnlyList<EmployeeWorkingDayDto> ToWeek(IEnumerable<EmployeeWorkingDay> days)
    {
        var byDay = days.ToDictionary(x => x.Day, StringComparer.OrdinalIgnoreCase);
        return WeekDays.Select(day => byDay.TryGetValue(day, out var value)
            ? new EmployeeWorkingDayDto(day, value.Enabled, value.Enabled ? value.FromTime : null, value.Enabled ? value.ToTime : null)
            : new EmployeeWorkingDayDto(day, false, null, null)).ToList();
    }

    public static IReadOnlyList<EmployeeWorkingDayDto> ToWeek(IEnumerable<EmployeeWorkingDayDto> days)
    {
        var byDay = days.ToDictionary(x => x.Day, StringComparer.OrdinalIgnoreCase);
        return WeekDays.Select(day => byDay[day]).ToList();
    }

    private static bool IsCompleteWeek(IReadOnlyList<WorkingDayDto>? days)
        => days is not null && days.Count == WeekDays.Length &&
           days.Select(x => x.Day).Distinct(StringComparer.OrdinalIgnoreCase).Count() == WeekDays.Length &&
           days.All(x => WeekDays.Contains(x.Day, StringComparer.OrdinalIgnoreCase));
}
