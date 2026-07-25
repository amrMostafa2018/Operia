using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Commands.SendPasswordOtp;

public sealed class SendPasswordOtpHandler : IRequestHandler<SendPasswordOtpCommand>
{
    private readonly IOtpService _otpService;
    private readonly ICurrentUserService _currentUserService;

    public SendPasswordOtpHandler(
        IOtpService otpService,
        ICurrentUserService currentUserService)
    {
        _otpService = otpService;
        _currentUserService = currentUserService;
    }

    public async Task Handle(SendPasswordOtpCommand request, CancellationToken cancellationToken)
    {
        var userId = SettingsHandlerHelpers.RequireUser(_currentUserService);
        await _otpService.GenerateAndSendOtpAsync(userId, cancellationToken);
    }
}
