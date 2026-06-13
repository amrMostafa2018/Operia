namespace Operia.Application.Auth.DTOs;

public sealed record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt);
