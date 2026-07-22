using MediatR;
using Operia.Application.Auth.DTOs;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Auth.Commands.CompleteFirstLogin;

public sealed class CompleteFirstLoginHandler(IIdentityService identity, ITokenService tokens) : IRequestHandler<CompleteFirstLoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(CompleteFirstLoginCommand request, CancellationToken ct)
    {
        await identity.CompleteFirstLoginAsync(request.UserId, request.ResetToken, request.NewPassword, ct);
        return await tokens.GenerateTokensAsync(request.UserId, ct);
    }
}
