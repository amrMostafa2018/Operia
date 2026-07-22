using MediatR;

namespace Operia.Application.Employees.Queries.ListEmployees;

public sealed class ListEmployeesHandler(IEmployeeService service) : IRequestHandler<ListEmployeesQuery, EmployeeListResult>
{
    public Task<EmployeeListResult> Handle(ListEmployeesQuery request, CancellationToken ct) => service.ListAsync(new(request.PageNumber, request.PageSize, request.Search, request.Role, request.IsActive, request.BranchId, request.CreatedFrom, request.CreatedTo), ct);
}
