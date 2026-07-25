using Operia.Application.Common.Models;

namespace Operia.Application.Employees;

public sealed record EmployeeDto(
    string Id, string Code, string FullName, string Email, string MobileNumber,
    string UserName, string? Specialty, string? JobTitle, DateOnly JoiningDate,
    string? PhotoUrl, bool IsActive, string Role, IReadOnlyList<EmployeeBranchDto> Branches,
    DateTime CreatedAt);

public sealed record EmployeeBranchDto(string Id, string Name);
public sealed record EmployeeRoleCountDto(string Role, int Count);
public sealed record EmployeeListResult(
    IReadOnlyList<EmployeeDto> Items, int PageNumber, int PageSize, int TotalCount,
    int TotalPages, IReadOnlyList<EmployeeRoleCountDto> RoleCounts);
public sealed record BookableEmployeeDto(string Id, string Code, string FullName, string? PhotoUrl, string? Specialty, string? JobTitle);

public sealed record EmployeeWorkingDayDto(string Day, bool Enabled, TimeOnly? FromTime, TimeOnly? ToTime);
public sealed record EmployeeScheduleDto(IReadOnlyList<EmployeeWorkingDayDto> Days);
