namespace Operia.Application.Employees.Queries.ListEmployees;

public sealed record EmployeeRoleCountDto(string Role, int Count);

public sealed record EmployeeListResult(
    IReadOnlyList<EmployeeDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<EmployeeRoleCountDto> RoleCounts);
