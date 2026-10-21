using Operia.Application.Employees;

namespace Operia.Application.Employees.Queries.GetBookableEmployees;

public sealed record BookableEmployeeDto(
    string Id,
    string Code,
    string FullName,
    string? PhotoUrl,
    string? Specialty,
    string? JobTitle,
    IReadOnlyList<EmployeeWorkingDayDto> WorkingDays);
