using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Operia.Application.Auth;
using Operia.Application.Common.Authorization;

namespace Operia.Infrastructure.Identity;

public static class PermissionClaimBootstrapper
{
    private const string BaselineVersionClaimType = "operia.permission-baseline";
    private const string BaselineVersion = "v1";

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> BaselinePermissions =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [Roles.SuperAdmin] =
            [
                Policies.DashboardRead,
                Policies.BookingsRead,
                Policies.BookingsManage,
                Policies.CustomersRead,
                Policies.CustomersManage,
                Policies.RevenueRead,
                Policies.RevenueReview,
                Policies.ReportsRead,
                Policies.EmployeesRead,
                Policies.EmployeesManage,
                Policies.PackagesRead,
                Policies.PackagesManage,
                Policies.BranchesRead,
                Policies.BranchesManage,
                Policies.SettingsManage,
                Policies.SubscriptionsRead,
                Policies.SubscriptionsManage,
                Policies.OnboardingManage
            ],
            [Roles.Admin] =
            [
                Policies.DashboardRead,
                Policies.BookingsRead,
                Policies.BookingsManage,
                Policies.CustomersRead,
                Policies.CustomersManage,
                Policies.RevenueRead,
                Policies.RevenueReview,
                Policies.ReportsRead,
                Policies.EmployeesRead,
                Policies.EmployeesManage,
                Policies.PackagesRead,
                Policies.PackagesManage,
                Policies.BranchesRead,
                Policies.BranchesManage,
                Policies.SubscriptionsRead
            ],
            [Roles.Reception] =
            [
                Policies.DashboardRead,
                Policies.BookingsRead,
                Policies.BookingsManage,
                Policies.CustomersRead,
                Policies.CustomersManage,
                Policies.PackagesRead,
                Policies.BranchesRead
            ],
            [Roles.Staff] =
            [
                Policies.BookingsRead
            ],
            [Roles.PlatformAdmin] =
            [
                Policies.Platform.Manage
            ]
        };

    public static async Task EnsureBaselinePermissionsAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var (roleName, permissions) in BaselinePermissions)
        {
            var role = await roleManager.FindByNameAsync(roleName)
                ?? throw new InvalidOperationException($"Role '{roleName}' was not found.");
            var claims = await roleManager.GetClaimsAsync(role);

            if (claims.Any(claim =>
                    claim.Type == BaselineVersionClaimType
                    && claim.Value == BaselineVersion))
            {
                continue;
            }

            foreach (var permission in permissions)
            {
                if (claims.Any(claim =>
                        claim.Type == Policies.PermissionClaimType
                        && claim.Value == permission))
                {
                    continue;
                }

                await roleManager.AddClaimAsync(
                    role,
                    new Claim(Policies.PermissionClaimType, permission));
            }

            await roleManager.AddClaimAsync(
                role,
                new Claim(BaselineVersionClaimType, BaselineVersion));
        }
    }

    public static IReadOnlyList<string> GetBaselinePermissions(string roleName) =>
        BaselinePermissions.TryGetValue(roleName, out var permissions)
            ? permissions
            : [];
}
