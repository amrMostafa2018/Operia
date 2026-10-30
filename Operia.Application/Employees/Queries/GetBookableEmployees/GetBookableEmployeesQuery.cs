using MediatR;

namespace Operia.Application.Employees.Queries.GetBookableEmployees;

public sealed record GetBookableEmployeesQuery(
    string BranchId,
    string? PackageId = null) : IRequest<IReadOnlyList<BookableEmployeeDto>>;
