using Operia.Application.Employees;

namespace Operia.Contracts.Employees.UpdateEmployeeSchedule;

public sealed record BranchScheduleRequest(
    string BranchId,
    IReadOnlyList<EmployeeWorkingDayDto> Days);

public sealed record UpdateEmployeeScheduleRequest(IReadOnlyList<BranchScheduleRequest> Branches);
