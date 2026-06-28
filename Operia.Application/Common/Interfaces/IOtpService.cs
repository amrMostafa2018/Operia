namespace Operia.Application.Common.Interfaces;

public interface IOtpService
{
    Task GenerateAndSendOtpAsync(string userId, CancellationToken cancellationToken = default);

    Task VerifyOtpAsync(string userId, string code, CancellationToken cancellationToken = default);
}
