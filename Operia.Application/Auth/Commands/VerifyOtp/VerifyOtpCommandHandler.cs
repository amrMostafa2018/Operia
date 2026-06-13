using MediatR;
using Operia.Application.Auth.DTOs;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Auth.Commands.VerifyOtp;

public sealed class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, AuthResponseDto>
{
    private readonly IOtpService _otpService;
    private readonly ITokenService _tokenService;

    public VerifyOtpCommandHandler(IOtpService otpService, ITokenService tokenService)
    {
        _otpService = otpService;
        _tokenService = tokenService;
    }

    public async Task<AuthResponseDto> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var isValid = await _otpService.VerifyOtpAsync(request.UserId, request.Code, cancellationToken);

        if (!isValid)
            throw new UnauthorizedException("Invalid or expired OTP code.");

        return await _tokenService.GenerateTokensAsync(request.UserId, cancellationToken);
    }
}
