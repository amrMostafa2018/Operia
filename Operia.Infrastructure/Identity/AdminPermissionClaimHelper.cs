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
        new(Permissions.ClaimType, Permissions.Admin.BranchesManage),
        new(Permissions.ClaimType, Permissions.Admin.EmployeesRead),
        new(Permissions.ClaimType, Permissions.Admin.EmployeesManage),
        new(Permissions.ClaimType, Permissions.Admin.PackagesRead),
        new(Permissions.ClaimType, Permissions.Admin.SettingsRead),
    ];

    public static async Task AddAdminPermissionClaimsAsync(
        RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in new[] { Roles.SuperAdmin, Roles.Admin })
        {
            var role = await roleManager.FindByNameAsync(roleName)
                ?? throw new InvalidOperationException($"Role '{roleName}' was not found.");
            var existingClaims = await roleManager.GetClaimsAsync(role);

            foreach (var claim in GetAdminPermissionClaims())
            {
                if (!existingClaims.Any(c => c.Type == claim.Type && c.Value == claim.Value))
                    await roleManager.AddClaimAsync(role, claim);
            }
        }
    }
}
