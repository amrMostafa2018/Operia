using MediatR;

namespace Operia.Application.Employees.Commands.ChangeEmployeeRole;

public sealed class ChangeEmployeeRoleHandler(IEmployeeService service) : IRequestHandler<ChangeEmployeeRoleCommand>
{
    public async Task Handle(ChangeEmployeeRoleCommand request, CancellationToken ct) => await service.ChangeRoleAsync(request.Id, request.Role, ct);
}
