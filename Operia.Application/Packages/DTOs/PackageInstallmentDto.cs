namespace Operia.Application.Packages.DTOs;

public sealed record PackageInstallmentDto(
    int Number,
    decimal Amount,
    int SessionsUnlocked,
    int CumulativeAvailableSessions);
