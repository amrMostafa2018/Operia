using MediatR;

namespace Operia.Application.Employees.Queries.GetEmployee;

public sealed record GetEmployeeQuery(string Id) : IRequest<EmployeeDto>;
