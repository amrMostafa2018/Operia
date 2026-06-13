using MediatR;
using Operia.Application.Auth.DTOs;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Auth.Commands.VerifyRegisterOtp;

public sealed class VerifyRegisterOtpCommandHandler
    : IRequestHandler<VerifyRegisterOtpCommand, AuthResponseDto>
{
    private readonly IRegistrationService _registrationService;
    private readonly ITokenService _tokenService;

    public VerifyRegisterOtpCommandHandler(
        IRegistrationService registrationService,
        ITokenService tokenService)
    {
        _registrationService = registrationService;
        _tokenService = tokenService;
    }

    public async Task<AuthResponseDto> Handle(
        VerifyRegisterOtpCommand request,
        CancellationToken cancellationToken)
    {
        var userId = await _registrationService.CompleteRegistrationAsync(
            request.RegistrationId,
            request.Code,
            cancellationToken);

        return await _tokenService.GenerateTokensAsync(userId, cancellationToken);
    }
}
