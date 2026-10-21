namespace Operia.Application.Employees;

public sealed record EmployeeWorkingDayDto(
    string Day,
    bool Enabled,
    TimeOnly? FromTime,
    TimeOnly? ToTime);

public sealed record EmployeeBranchScheduleDto(
    string BranchId,
    string BranchName,
    IReadOnlyList<EmployeeWorkingDayDto> Days);

public sealed record EmployeeScheduleDto(IReadOnlyList<EmployeeBranchScheduleDto> Branches);
