using MediatR;

namespace Operia.Application.Employees.Commands.UpdateEmployeeSchedule;

public sealed record UpdateEmployeeScheduleCommand(string EmployeeId, IReadOnlyList<EmployeeWorkingDayDto> Days)
    : IRequest<EmployeeScheduleDto>;
