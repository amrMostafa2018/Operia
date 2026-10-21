using MediatR;

namespace Operia.Application.Employees.Commands.UpdateEmployeeSchedule;

public sealed record EmployeeBranchScheduleInput(
    string BranchId,
    IReadOnlyList<EmployeeWorkingDayDto> Days);

public sealed record UpdateEmployeeScheduleCommand(
    string EmployeeId,
    IReadOnlyList<EmployeeBranchScheduleInput> Branches)
    : IRequest<EmployeeScheduleDto>;
