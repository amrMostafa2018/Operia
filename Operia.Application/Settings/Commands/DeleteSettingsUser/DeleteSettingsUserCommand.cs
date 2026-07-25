using MediatR;

namespace Operia.Application.Settings.Commands.DeleteSettingsUser;

public sealed record DeleteSettingsUserCommand(string UserId) : IRequest;
