using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Commands.BanSettingsUser;

public sealed class BanSettingsUserHandler : IRequestHandler<BanSettingsUserCommand>
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;

    public BanSettingsUserHandler(
        IIdentityService identityService,
        ICurrentUserService currentUserService)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
    }

    public async Task Handle(BanSettingsUserCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = SettingsHandlerHelpers.RequireUser(_currentUserService);
        await _identityService.BanTenantUserAsync(currentUserId, request.UserId, cancellationToken);
    }
}
