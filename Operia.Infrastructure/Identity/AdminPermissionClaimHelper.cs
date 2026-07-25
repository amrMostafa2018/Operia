using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Operia.Application.Auth;
using Operia.Application.Common.Authorization;

namespace Operia.Infrastructure.Identity;

public static class AdminPermissionClaimHelper
{
    public static IEnumerable<Claim> GetAdminPermissionClaims() =>
    [
        new(Policies.PermissionClaimType, Policies.DashboardRead),
        new(Policies.PermissionClaimType, Policies.BookingsRead),
        new(Policies.PermissionClaimType, Policies.BookingsManage),
        new(Policies.PermissionClaimType, Policies.CustomersRead),
        new(Policies.PermissionClaimType, Policies.CustomersManage),
        new(Policies.PermissionClaimType, Policies.RevenueRead),
        new(Policies.PermissionClaimType, Policies.RevenueReview),
        new(Policies.PermissionClaimType, Policies.ReportsRead),
        new(Policies.PermissionClaimType, Policies.EmployeesRead),
        new(Policies.PermissionClaimType, Policies.EmployeesManage),
        new(Policies.PermissionClaimType, Policies.PackagesRead),
        new(Policies.PermissionClaimType, Policies.PackagesManage),
        new(Policies.PermissionClaimType, Policies.BranchesRead),
        new(Policies.PermissionClaimType, Policies.BranchesManage),
        new(Policies.PermissionClaimType, Policies.SubscriptionsRead),
    ];

    public static async Task AddAdminPermissionClaimsAsync(
        RoleManager<IdentityRole> roleManager)
    {
        // 1. Reconcile SuperAdmin: remove all permission claims (SuperAdmin bypasses claims)
        var superAdminRole = await roleManager.FindByNameAsync(Roles.SuperAdmin)
            ?? throw new InvalidOperationException($"Role '{Roles.SuperAdmin}' was not found.");
        var superAdminClaims = await roleManager.GetClaimsAsync(superAdminRole);
        foreach (var claim in superAdminClaims.Where(c => c.Type == Policies.PermissionClaimType))
        {
            await roleManager.RemoveClaimAsync(superAdminRole, claim);
        }

        // 2. Reconcile Admin: ensure exactly the 15 eligible default claims are present and remove obsolete/ineligible permission claims
        var adminRole = await roleManager.FindByNameAsync(Roles.Admin)
            ?? throw new InvalidOperationException($"Role '{Roles.Admin}' was not found.");
        var adminClaims = await roleManager.GetClaimsAsync(adminRole);
        var targetClaims = GetAdminPermissionClaims().ToList();
        var targetValues = new HashSet<string>(targetClaims.Select(c => c.Value), StringComparer.Ordinal);

        foreach (var claim in adminClaims.Where(c => c.Type == Policies.PermissionClaimType))
        {
            if (!targetValues.Contains(claim.Value))
            {
                await roleManager.RemoveClaimAsync(adminRole, claim);
            }
        }

        foreach (var claim in targetClaims)
        {
            if (!adminClaims.Any(c => c.Type == claim.Type && c.Value == claim.Value))
            {
                await roleManager.AddClaimAsync(adminRole, claim);
            }
        }
    }
}
