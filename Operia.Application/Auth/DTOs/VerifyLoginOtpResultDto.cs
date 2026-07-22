namespace Operia.Application.Auth.DTOs;

public sealed record VerifyLoginOtpResultDto(
    bool RequiresPasswordChange,
    string? ResetToken,
    string? AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAt);
