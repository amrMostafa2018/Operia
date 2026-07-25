using MediatR;

namespace Operia.Application.Employees.Queries.GetEmployeeSchedule;

public sealed record GetEmployeeScheduleQuery(string EmployeeId) : IRequest<EmployeeScheduleDto>;
