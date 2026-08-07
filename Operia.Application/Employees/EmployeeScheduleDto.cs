namespace Operia.Application.Employees;

public sealed record EmployeeWorkingDayDto(
    string Day,
    bool Enabled,
    TimeOnly? FromTime,
    TimeOnly? ToTime);

public sealed record EmployeeScheduleDto(IReadOnlyList<EmployeeWorkingDayDto> Days);
