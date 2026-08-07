using Microsoft.AspNetCore.Identity;
using Operia.Application.Auth;
using Operia.Application.Common.Authorization;

namespace Operia.Infrastructure.Identity;

public sealed class IdentityPermissionGrantStore(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) : IPermissionGrantStore
{
    public async Task<bool> HasPermissionAsync(
        string userId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return false;
        }

        var userClaims = await userManager.GetClaimsAsync(user);
        if (HasPermission(userClaims, permission))
        {
            return true;
        }

        var roleNames = await userManager.GetRolesAsync(user);
        foreach (var roleName in roleNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            var roleClaims = await roleManager.GetClaimsAsync(role);
            if (HasPermission(roleClaims, permission))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPermission(IEnumerable<System.Security.Claims.Claim> claims, string permission) =>
        claims.Any(claim =>
            claim.Type == Policies.PermissionClaimType
            && string.Equals(claim.Value, permission, StringComparison.Ordinal));
}
