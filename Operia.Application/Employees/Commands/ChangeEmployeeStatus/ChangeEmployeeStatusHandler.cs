using MediatR;

namespace Operia.Application.Employees.Commands.ChangeEmployeeStatus;

public sealed class ChangeEmployeeStatusHandler(IEmployeeService service) : IRequestHandler<ChangeEmployeeStatusCommand>
{
    public async Task Handle(ChangeEmployeeStatusCommand request, CancellationToken ct) => await service.ChangeStatusAsync(request.Id, request.IsActive, ct);
}
