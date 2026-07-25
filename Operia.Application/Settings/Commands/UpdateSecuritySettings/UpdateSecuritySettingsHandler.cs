using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Commands.UpdateSecuritySettings;

public sealed class UpdateSecuritySettingsHandler : IRequestHandler<UpdateSecuritySettingsCommand>
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;

    public UpdateSecuritySettingsHandler(
        IIdentityService identityService,
        ICurrentUserService currentUserService)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
    }

    public async Task Handle(UpdateSecuritySettingsCommand request, CancellationToken cancellationToken)
    {
        var userId = SettingsHandlerHelpers.RequireUser(_currentUserService);
        await _identityService.UpdateSecurityUserSettingsAsync(
            userId,
            request.Request.EnableTwoFactorAuthentication,
            request.Request.LoginAlertsEnabled,
            request.Request.LogoutOtherDevices,
            cancellationToken);
    }
}
