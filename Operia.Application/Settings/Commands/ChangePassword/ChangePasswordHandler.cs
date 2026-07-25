using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Commands.ChangePassword;

public sealed class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IOtpService _otpService;
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;

    public ChangePasswordHandler(
        IOtpService otpService,
        IIdentityService identityService,
        ICurrentUserService currentUserService)
    {
        _otpService = otpService;
        _identityService = identityService;
        _currentUserService = currentUserService;
    }

    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = SettingsHandlerHelpers.RequireUser(_currentUserService);
        await _otpService.VerifyOtpAsync(userId, request.Request.OtpCode, cancellationToken);
        await _identityService.ChangePasswordAsync(userId, request.Request.CurrentPassword, request.Request.NewPassword, cancellationToken);
    }
}
