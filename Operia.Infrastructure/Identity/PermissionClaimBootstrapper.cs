using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Operia.Application.Auth;
using Operia.Application.Common.Authorization;

namespace Operia.Infrastructure.Identity;

public static class PermissionClaimBootstrapper
{
    private const string BaselineVersionClaimType = "operia.permission-baseline";
    private const string BaselineVersion = "v5";

    public static readonly IReadOnlySet<string> AdminExcludedPermissions =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Policies.SettingsManage,
            Policies.SettingsIdentityManage,
            Policies.SettingsPaymentsManage,
            Policies.SettingsWorkingDaysManage,
            Policies.SettingsSecurityManage,
            Policies.SettingsPasswordChange,
            Policies.SettingsUsersBan,
            Policies.SettingsUsersDelete,
            Policies.SettingsAccountDeactivate,
            Policies.SettingsNotificationsManage,
            Policies.SettingsLanguageManage,
            Policies.SubscriptionsManage,
            Policies.OnboardingManage
        };

    private static readonly IReadOnlyList<string> ReceptionPermissions =
    [
        Policies.DashboardRead,
        Policies.DashboardExport,
        Policies.BookingsRead,
        Policies.BookingsManage,
        Policies.BookingsExport,
        Policies.BookingsCancel,
        Policies.BookingsReassign,
        Policies.BookingsChangeStatus,
        Policies.CustomersRead,
        Policies.CustomersManage,
        Policies.CustomersExport,
        Policies.CustomersActivatePackage,
        Policies.CustomersCancelPackage,
        Policies.CustomersAddPackage,
        Policies.CustomersCollectInstallment,
        Policies.PackagesRead,
        Policies.PackagesSell,
        Policies.OffersRead,
        Policies.BranchesRead,
        Policies.SupportRead,
        Policies.NotificationsRead
    ];

    private static readonly IReadOnlyList<string> StaffPermissions =
    [
        Policies.BookingsRead,
        Policies.SupportRead,
        Policies.NotificationsRead
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> BaselinePermissions =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [Roles.SuperAdmin] = Policies.TenantPermissionValues,
            [Roles.Admin] = Policies.TenantPermissionValues
                .Where(permission => !AdminExcludedPermissions.Contains(permission))
                .ToArray(),
            [Roles.Reception] = ReceptionPermissions,
            [Roles.Staff] = StaffPermissions,
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

            var priorBaseline = claims.FirstOrDefault(claim => claim.Type == BaselineVersionClaimType);
            if (priorBaseline?.Value == BaselineVersion)
            {
                continue;
            }

            // Upgrades add only newly introduced grants. Existing customizations remain.
            if (priorBaseline is not null && priorBaseline.Value is not ("v3" or "v4"))
            {
                continue;
            }
            var additions = priorBaseline is null
                ? permissions
                : roleName is Roles.Admin or Roles.SuperAdmin
                    ? new[] { Policies.CustomersCollectInstallment,
                        Policies.CustomersRefund, Policies.CustomersBlock,
                        Policies.CustomersHistory }
                    : roleName is Roles.Reception
                        ? new[] { Policies.CustomersCollectInstallment }
                        : [];

            foreach (var permission in additions)
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

            if (priorBaseline is not null)
            {
                await roleManager.RemoveClaimAsync(role, priorBaseline);
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
