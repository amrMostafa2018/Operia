namespace Operia.Application.Settings.Commands.UpdateSecuritySettings;

public sealed record UpdateSecurityRequest(
    bool EnableTwoFactorAuthentication,
    bool LoginAlertsEnabled,
    bool LogoutOtherDevices);
