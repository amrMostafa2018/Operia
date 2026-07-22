using MediatR;
using Operia.Application.Common.Models;

namespace Operia.Application.Employees.Commands.CreateEmployee;

public sealed record CreateEmployeeCommand(string FullName, string Email, string MobileNumber, string UserName, string? Specialty, string? JobTitle, DateOnly JoiningDate, bool IsActive, string Role, IReadOnlyList<string> BranchIds, string TemporaryPassword, FileUploadContent? Photo) : IRequest<EmployeeDto>;
