using MediatR;

namespace Operia.Application.Employees.Queries.GetEmployee;

public sealed class GetEmployeeHandler(IEmployeeService service) : IRequestHandler<GetEmployeeQuery, EmployeeDto>
{
    public Task<EmployeeDto> Handle(GetEmployeeQuery request, CancellationToken ct) => service.GetAsync(request.Id, ct);
}
