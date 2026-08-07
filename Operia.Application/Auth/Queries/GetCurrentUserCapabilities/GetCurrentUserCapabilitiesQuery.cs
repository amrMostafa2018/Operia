using MediatR;

namespace Operia.Application.Auth.Queries.GetCurrentUserCapabilities;

public sealed record GetCurrentUserCapabilitiesQuery : IRequest<CurrentUserCapabilitiesDto>;
