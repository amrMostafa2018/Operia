namespace Operia.Application.Onboarding.DTOs;

public sealed record PendingAddBalancePlatformDto(
    string Id,
    decimal Amount,
    string ScreenShotUrl);
