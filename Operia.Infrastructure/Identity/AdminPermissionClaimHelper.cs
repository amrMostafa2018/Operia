using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Operia.Application.Common.Authorization;

namespace Operia.Infrastructure.Identity;

internal static class AdminPermissionClaimHelper
{
    public static IEnumerable<Claim> GetAdminPermissionClaims() =>
    [
        new(Permissions.ClaimType, Permissions.Admin.DashboardRead),
        new(Permissions.ClaimType, Permissions.Admin.BookingRead),
        new(Permissions.ClaimType, Permissions.Admin.CustomersRead),
        new(Permissions.ClaimType, Permissions.Admin.ReportsRead),
        new(Permissions.ClaimType, Permissions.Admin.BranchesRead),
        new(Permissions.ClaimType, Permissions.Admin.EmployeesRead),
        new(Permissions.ClaimType, Permissions.Admin.PackagesRead),
        new(Permissions.ClaimType, Permissions.Admin.SettingsRead),
    ];

    public static async Task AddAdminPermissionClaimsAsync(
        RoleManager<IdentityRole> roleManager)
    {
        var role = await roleManager.FindByNameAsync(Roles.Admin)
            ?? throw new InvalidOperationException($"Role '{Roles.Admin}' was not found.");

        var existingClaims = await roleManager.GetClaimsAsync(role);

        foreach (var claim in GetAdminPermissionClaims())
        {
            if (existingClaims.Any(c => c.Type == claim.Type && c.Value == claim.Value))
                continue;

            await roleManager.AddClaimAsync(role, claim);
        }
    }
}
