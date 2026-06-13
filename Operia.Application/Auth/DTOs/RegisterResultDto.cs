namespace Operia.Application.Auth.DTOs;

public sealed record RegisterResultDto(
    bool RequiresOtp,
    string RegistrationId);
