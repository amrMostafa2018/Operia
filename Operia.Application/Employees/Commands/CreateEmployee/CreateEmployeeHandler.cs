using MediatR;

namespace Operia.Application.Employees.Commands.CreateEmployee;

public sealed class CreateEmployeeHandler(IEmployeeService service) : IRequestHandler<CreateEmployeeCommand, EmployeeDto>
{
    public Task<EmployeeDto> Handle(CreateEmployeeCommand request, CancellationToken ct) => service.CreateAsync(new(request.FullName, request.Email, request.MobileNumber, request.UserName, request.Specialty, request.JobTitle, request.JoiningDate, request.IsActive, request.Role, request.BranchIds, request.TemporaryPassword, request.Photo, false), ct);
}
