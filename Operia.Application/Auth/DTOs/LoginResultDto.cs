namespace Operia.Application.Auth.DTOs;

public sealed record LoginResultDto(
    bool RequiresOtp,
    string UserId);
