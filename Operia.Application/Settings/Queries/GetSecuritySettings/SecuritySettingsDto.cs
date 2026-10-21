namespace Operia.Application.Settings.Queries.GetSecuritySettings;

public sealed record AuthorizedUserDto(
    string Id,
    string Name,
    string Email,
    bool IsBanned);

public sealed record SecuritySettingsDto(
    bool EnableTwoFactorAuthentication,
    bool LoginAlertsEnabled,
    string PhoneNumber,
    IReadOnlyList<AuthorizedUserDto> Users);
