namespace Operia.Application.Common.Interfaces;

public interface IOtpSender
{
    Task SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken = default);
}
