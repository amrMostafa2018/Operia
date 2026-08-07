using Microsoft.AspNetCore.Identity;
using Operia.Application.Auth;
using Operia.Application.Common.Authorization;

namespace Operia.Infrastructure.Identity;

public sealed class IdentityPermissionGrantStore(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) : IPermissionGrantStore
{
    public async Task<PermissionGrantSnapshot?> GetUserCapabilitiesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        AddPermissionClaims(await userManager.GetClaimsAsync(user), permissions);

        foreach (var roleName in roles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var role = await roleManager.FindByNameAsync(roleName);
            if (role is not null)
            {
                AddPermissionClaims(await roleManager.GetClaimsAsync(role), permissions);
            }
        }

        return new PermissionGrantSnapshot(
            roles.OrderBy(role => role, StringComparer.Ordinal).ToArray(),
            permissions.OrderBy(permission => permission, StringComparer.Ordinal).ToArray());
    }

    public async Task<bool> HasPermissionAsync(
        string userId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var capabilities = await GetUserCapabilitiesAsync(userId, cancellationToken);
        return capabilities?.Permissions.Contains(permission, StringComparer.Ordinal) == true;
    }

    private static void AddPermissionClaims(
        IEnumerable<System.Security.Claims.Claim> claims,
        ISet<string> permissions)
    {
        foreach (var claim in claims.Where(claim => claim.Type == Policies.PermissionClaimType))
        {
            permissions.Add(claim.Value);
        }
    }
}
