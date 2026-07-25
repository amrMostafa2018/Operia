using MediatR;

namespace Operia.Application.Settings.Queries.GetIdentitySettings;

public sealed record GetIdentitySettingsQuery : IRequest<IdentitySettingsDto>;
