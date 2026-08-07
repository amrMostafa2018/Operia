namespace Operia.Application.Auth.Queries.GetCurrentUserCapabilities;

public sealed record CurrentUserCapabilitiesDto(
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
