using MediatR;

namespace Operia.Application.Employees.Queries.GetBookableEmployees;

public sealed class GetBookableEmployeesHandler(IEmployeeService service) : IRequestHandler<GetBookableEmployeesQuery, IReadOnlyList<BookableEmployeeDto>>
{
    public Task<IReadOnlyList<BookableEmployeeDto>> Handle(GetBookableEmployeesQuery request, CancellationToken ct) => service.BookableAsync(request.BranchId, ct);
}
