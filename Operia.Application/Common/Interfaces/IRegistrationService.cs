namespace Operia.Application.Common.Interfaces;

public interface IRegistrationService
{
    Task<Auth.DTOs.RegisterResultDto> InitiateRegistrationAsync(
        string fullName,
        string email,
        string password,
        string phoneNumber,
        CancellationToken cancellationToken = default);

    Task<string> CompleteRegistrationAsync(
        string registrationId,
        string code,
        CancellationToken cancellationToken = default);

    Task ResendRegistrationOtpAsync(
        string registrationId,
        CancellationToken cancellationToken = default);
}
