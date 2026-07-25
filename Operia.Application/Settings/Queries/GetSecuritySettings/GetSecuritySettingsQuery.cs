using MediatR;

namespace Operia.Application.Settings.Queries.GetSecuritySettings;

public sealed record GetSecuritySettingsQuery : IRequest<SecuritySettingsDto>;
