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
        UserManager<ApplicationUser> userManager,
        ApplicationUser user)
    {
        foreach (var claim in GetAdminPermissionClaims())
            await userManager.AddClaimAsync(user, claim);
    }
}
