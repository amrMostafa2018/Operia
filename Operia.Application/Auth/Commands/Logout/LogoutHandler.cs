using MediatR;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Auth.Commands.Logout;

public sealed class LogoutHandler : IRequestHandler<LogoutCommand>
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;

    public LogoutHandler(
        IIdentityService identityService,
        ICurrentUserService currentUserService)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedException("User is not authenticated.");

        await _identityService.LogoutAsync(userId, cancellationToken);
    }
}
