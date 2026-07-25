using MediatR;

namespace Operia.Application.Settings.Commands.UpdateIdentitySettings;

public sealed record UpdateIdentitySettingsCommand(UpdateIdentitySettingsRequest Request) : IRequest<IdentitySettingsDto>;
