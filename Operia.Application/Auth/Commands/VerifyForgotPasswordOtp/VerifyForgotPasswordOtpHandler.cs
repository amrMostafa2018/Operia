using MediatR;
using Operia.Application.Auth.DTOs;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.PhoneNumbers;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Auth.Commands.VerifyForgotPasswordOtp;

public sealed class VerifyForgotPasswordOtpHandler
    : IRequestHandler<VerifyForgotPasswordOtpCommand, VerifyForgotPasswordOtpResultDto>
{
    private readonly IIdentityService _identityService;
    private readonly IOtpService _otpService;

    public VerifyForgotPasswordOtpHandler(IIdentityService identityService, IOtpService otpService)
    {
        _identityService = identityService;
        _otpService = otpService;
    }

    public async Task<VerifyForgotPasswordOtpResultDto> Handle(
        VerifyForgotPasswordOtpCommand request,
        CancellationToken cancellationToken)
    {
        var phoneNumber = PhoneNumberHelper.ToE164(request.PhoneNumber);
        var userId = await _identityService.GetUserIdByPhoneAsync(phoneNumber, cancellationToken);

        if (userId is null)
            throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.OtpInvalid, "code");

        await _otpService.VerifyResetOtpAsync(userId, request.OtpCode, cancellationToken);

        var resetToken = await _identityService.GeneratePasswordResetTokenAsync(userId, cancellationToken);

        return new VerifyForgotPasswordOtpResultDto(resetToken);
    }
}
