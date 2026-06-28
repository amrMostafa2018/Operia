using MediatR;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Auth.Commands.ResendLoginOtp;

public sealed class ResendLoginOtpCommandHandler : IRequestHandler<ResendLoginOtpCommand>
{
    private readonly IOtpService _otpService;

    public ResendLoginOtpCommandHandler(IOtpService otpService)
    {
        _otpService = otpService;
    }

    public Task Handle(ResendLoginOtpCommand request, CancellationToken cancellationToken) =>
        _otpService.GenerateAndSendOtpAsync(request.UserId, cancellationToken);
}
