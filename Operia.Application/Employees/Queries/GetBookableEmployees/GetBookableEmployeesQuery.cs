using MediatR;

namespace Operia.Application.Employees.Queries.GetBookableEmployees;

public sealed record GetBookableEmployeesQuery(string BranchId) : IRequest<IReadOnlyList<BookableEmployeeDto>>;
