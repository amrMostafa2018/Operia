using MediatR;

namespace Operia.Application.Employees.Commands.UpdateEmployee;

public sealed class UpdateEmployeeHandler(IEmployeeService service) : IRequestHandler<UpdateEmployeeCommand, EmployeeDto>
{
    public Task<EmployeeDto> Handle(UpdateEmployeeCommand request, CancellationToken ct) => service.UpdateAsync(request.Id, new(request.FullName, request.Email, request.MobileNumber, request.UserName, request.Specialty, request.JobTitle, request.JoiningDate, request.IsActive, request.Role, request.BranchIds, null, request.Photo, request.RemovePhoto), ct);
}
