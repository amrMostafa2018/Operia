using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Queries.GetSecuritySettings;

public sealed class GetSecuritySettingsHandler : IRequestHandler<GetSecuritySettingsQuery, SecuritySettingsDto>
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;

    public GetSecuritySettingsHandler(
        IIdentityService identityService,
        ICurrentUserService currentUserService)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
    }

    public async Task<SecuritySettingsDto> Handle(GetSecuritySettingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = SettingsHandlerHelpers.RequireTenant(_currentUserService);
        var userId = SettingsHandlerHelpers.RequireUser(_currentUserService);

        var userSettings = await _identityService.GetSecurityUserSettingsAsync(userId, cancellationToken);
        var tenantUsers = await _identityService.GetTenantUsersAsync(tenantId, cancellationToken);

        var list = tenantUsers
            .Select(x => new AuthorizedUserDto(x.Id, x.UserName, x.Email, x.IsLockedOut))
            .ToList();

        return new SecuritySettingsDto(
            userSettings.TwoFactorEnabled,
            userSettings.LoginAlertsEnabled,
            SettingsHandlerHelpers.Mask(userSettings.PhoneNumber),
            list);
    }
}
