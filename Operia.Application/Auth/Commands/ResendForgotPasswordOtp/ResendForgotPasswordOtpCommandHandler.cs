using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.PhoneNumbers;

namespace Operia.Application.Auth.Commands.ResendForgotPasswordOtp;

public sealed class ResendForgotPasswordOtpCommandHandler : IRequestHandler<ResendForgotPasswordOtpCommand>
{
    private readonly IIdentityService _identityService;
    private readonly IOtpService _otpService;

    public ResendForgotPasswordOtpCommandHandler(IIdentityService identityService, IOtpService otpService)
    {
        _identityService = identityService;
        _otpService = otpService;
    }

    public async Task Handle(ResendForgotPasswordOtpCommand request, CancellationToken cancellationToken)
    {
        var phoneNumber = PhoneNumberHelper.ToE164(request.PhoneNumber);
        var userId = await _identityService.GetUserIdByPhoneAsync(phoneNumber, cancellationToken);

        if (userId is null)
            return;

        await _otpService.GenerateAndSendResetOtpAsync(userId, cancellationToken);
    }
}
