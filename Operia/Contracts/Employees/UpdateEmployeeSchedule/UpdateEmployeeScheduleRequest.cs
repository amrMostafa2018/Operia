using Operia.Application.Employees;

namespace Operia.Contracts.Employees.UpdateEmployeeSchedule;

public sealed record UpdateEmployeeScheduleRequest(IReadOnlyList<EmployeeWorkingDayDto> Days);
