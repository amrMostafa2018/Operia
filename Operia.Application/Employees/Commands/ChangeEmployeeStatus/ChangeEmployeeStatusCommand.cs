using MediatR;

namespace Operia.Application.Employees.Commands.ChangeEmployeeStatus;

public sealed record ChangeEmployeeStatusCommand(string Id, bool IsActive) : IRequest;
