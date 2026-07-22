using MediatR;

namespace Operia.Application.Employees.Commands.ChangeEmployeeRole;

public sealed record ChangeEmployeeRoleCommand(string Id, string Role) : IRequest;
