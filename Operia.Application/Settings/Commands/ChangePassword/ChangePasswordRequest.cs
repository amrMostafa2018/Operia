namespace Operia.Application.Settings.Commands.ChangePassword;

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string OtpCode);
