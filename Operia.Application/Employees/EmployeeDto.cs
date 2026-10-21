namespace Operia.Application.Employees;

public sealed record EmployeeDto(
    string Id,
    string Code,
    string FullName,
    string Email,
    string MobileNumber,
    string UserName,
    string? Specialty,
    string? JobTitle,
    DateOnly JoiningDate,
    string? PhotoUrl,
    bool IsActive,
    string Role,
    IReadOnlyList<EmployeeBranchDto> Branches,
    DateTime CreatedAt);
