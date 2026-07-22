using MediatR;
using Operia.Application.Auth.DTOs;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Auth.Commands.VerifyOtp;

public sealed class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, VerifyLoginOtpResultDto>
{
    private readonly IOtpService _otpService;
    private readonly ITokenService _tokenService;
    private readonly IIdentityService _identityService;

    public VerifyOtpCommandHandler(IOtpService otpService, ITokenService tokenService, IIdentityService identityService)
    {
        _otpService = otpService;
        _tokenService = tokenService;
        _identityService = identityService;
    }

    public async Task<VerifyLoginOtpResultDto> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        await _otpService.VerifyOtpAsync(request.UserId, request.Code, cancellationToken);

        if (await _identityService.MustChangePasswordAsync(request.UserId, cancellationToken))
        {
            var resetToken = await _identityService.GeneratePasswordResetTokenAsync(request.UserId, cancellationToken);
            return new(true, resetToken, null, null, null);
        }
        var tokens = await _tokenService.GenerateTokensAsync(request.UserId, cancellationToken);
        return new(false, null, tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresAt);
    }
}
