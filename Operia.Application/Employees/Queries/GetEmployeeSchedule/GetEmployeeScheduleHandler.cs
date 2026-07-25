using MediatR;

namespace Operia.Application.Employees.Queries.GetEmployeeSchedule;

public sealed class GetEmployeeScheduleHandler(IEmployeeScheduleService service)
    : IRequestHandler<GetEmployeeScheduleQuery, EmployeeScheduleDto>
{
    public Task<EmployeeScheduleDto> Handle(GetEmployeeScheduleQuery request, CancellationToken ct)
        => service.GetAsync(request.EmployeeId, ct);
}
