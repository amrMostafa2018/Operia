using MediatR;
using Operia.Application.Auth.DTOs;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    private readonly ITokenService _tokenService;

    public RefreshTokenCommandHandler(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public async Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var result = await _tokenService.RefreshTokenAsync(request.RefreshToken, cancellationToken);

        if (result is null)
            throw new UnauthorizedException("Invalid or expired refresh token.");

        return result;
    }
}
