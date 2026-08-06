using MediatR;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Auth.Commands.ResendLoginOtp;

public sealed class ResendLoginOtpHandler : IRequestHandler<ResendLoginOtpCommand>
{
    private readonly IOtpService _otpService;

    public ResendLoginOtpHandler(IOtpService otpService)
    {
        _otpService = otpService;
    }

    public Task Handle(ResendLoginOtpCommand request, CancellationToken cancellationToken) =>
        _otpService.GenerateAndSendOtpAsync(request.UserId, cancellationToken);
}
