using MediatR;

namespace Operia.Application.Employees.Commands.UpdateEmployeeSchedule;

public sealed class UpdateEmployeeScheduleHandler(IEmployeeScheduleService service)
    : IRequestHandler<UpdateEmployeeScheduleCommand, EmployeeScheduleDto>
{
    public Task<EmployeeScheduleDto> Handle(UpdateEmployeeScheduleCommand request, CancellationToken ct)
        => service.UpdateAsync(request.EmployeeId, request.Days, ct);
}
