using MediatR;

namespace Operia.Application.Settings.Commands.BanSettingsUser;

public sealed record BanSettingsUserCommand(string UserId) : IRequest;
