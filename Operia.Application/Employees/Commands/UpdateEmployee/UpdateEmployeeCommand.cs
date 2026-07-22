using MediatR;
using Operia.Application.Common.Models;

namespace Operia.Application.Employees.Commands.UpdateEmployee;

public sealed record UpdateEmployeeCommand(string Id, string FullName, string Email, string MobileNumber, string UserName, string? Specialty, string? JobTitle, DateOnly JoiningDate, bool IsActive, string Role, IReadOnlyList<string> BranchIds, FileUploadContent? Photo, bool RemovePhoto) : IRequest<EmployeeDto>;
