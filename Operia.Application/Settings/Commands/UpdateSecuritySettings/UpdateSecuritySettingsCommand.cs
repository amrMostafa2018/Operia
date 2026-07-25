using MediatR;

namespace Operia.Application.Settings.Commands.UpdateSecuritySettings;

public sealed record UpdateSecuritySettingsCommand(UpdateSecurityRequest Request) : IRequest;
