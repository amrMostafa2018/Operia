using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;

namespace Operia.Application.Settings.Queries.GetSecuritySettings;

public sealed class GetSecuritySettingsHandler : IRequestHandler<GetSecuritySettingsQuery, SecuritySettingsDto>
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _db;

    public GetSecuritySettingsHandler(
        IIdentityService identityService,
        ICurrentUserService currentUserService,
        IApplicationDbContext db)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
        _db = db;
    }

    public async Task<SecuritySettingsDto> Handle(GetSecuritySettingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = SettingsHandlerHelpers.RequireTenant(_currentUserService);
        var userId = SettingsHandlerHelpers.RequireUser(_currentUserService);

        var userSettings = await _identityService.GetSecurityUserSettingsAsync(userId, cancellationToken);
        var ownerUserId = await _db.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => tenant.OwnerUserId)
            .FirstOrDefaultAsync(cancellationToken);

        UserSummaryDto? owner = null;
        if (!string.IsNullOrWhiteSpace(ownerUserId))
        {
            owner = await _identityService.GetUserSummaryAsync(ownerUserId, cancellationToken);
        }

        var tenantUsers = await _identityService.GetTenantUsersAsync(
            tenantId,
            ownerUserId,
            cancellationToken);

        var list = tenantUsers
            .Where(user => !IsTenantCreator(user, ownerUserId, owner))
            .Select(user => new AuthorizedUserDto(
                user.Id,
                user.UserName,
                user.Email,
                user.IsLockedOut))
            .ToList();

        return new SecuritySettingsDto(
            userSettings.TwoFactorEnabled,
            userSettings.LoginAlertsEnabled,
            userSettings.PhoneNumber ?? string.Empty,
            list);
    }

    private static bool IsTenantCreator(
        UserSummaryDto user,
        string? ownerUserId,
        UserSummaryDto? owner)
    {
        if (!string.IsNullOrWhiteSpace(ownerUserId) && user.Id == ownerUserId)
        {
            return true;
        }

        if (owner is null)
        {
            return false;
        }

        return HasSameLogin(user.UserName, owner.UserName)
            || HasSameLogin(user.UserName, owner.PhoneNumber);
    }

    private static bool HasSameLogin(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        if (string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var leftDigits = DigitsOnly(left);
        var rightDigits = DigitsOnly(right);
        return leftDigits.Length >= 8 && leftDigits == rightDigits;
    }

    private static string DigitsOnly(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }
}
