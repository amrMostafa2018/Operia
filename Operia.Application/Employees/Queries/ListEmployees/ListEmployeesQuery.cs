using MediatR;

namespace Operia.Application.Employees.Queries.ListEmployees;

public sealed record ListEmployeesQuery(int PageNumber, int PageSize, string? Search, string? Role, bool? IsActive, string? BranchId, DateOnly? CreatedFrom, DateOnly? CreatedTo) : IRequest<EmployeeListResult>;
