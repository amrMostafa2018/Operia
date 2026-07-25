using Microsoft.AspNetCore.Authorization;
using Operia.Application.Auth;
using Operia.Application.Common.Authorization;

namespace Operia.Infrastructure.Authorization;

public sealed record TenantPolicyRule(
    string PolicyName,
    bool AllowAdminWithPermission,
    bool AllowReception,
    bool AllowStaff
);

public static class PolicyDefinitions
{
    public static readonly IReadOnlyList<TenantPolicyRule> TenantPolicyMatrix = new List<TenantPolicyRule>
    {
        new(Policies.DashboardRead, AllowAdminWithPermission: true, AllowReception: true, AllowStaff: false),
        new(Policies.BookingsRead, AllowAdminWithPermission: true, AllowReception: true, AllowStaff: true),
        new(Policies.BookingsManage, AllowAdminWithPermission: true, AllowReception: true, AllowStaff: false),
        new(Policies.CustomersRead, AllowAdminWithPermission: true, AllowReception: true, AllowStaff: false),
        new(Policies.CustomersManage, AllowAdminWithPermission: true, AllowReception: true, AllowStaff: false),
        new(Policies.RevenueRead, AllowAdminWithPermission: true, AllowReception: false, AllowStaff: false),
        new(Policies.RevenueReview, AllowAdminWithPermission: true, AllowReception: false, AllowStaff: false),
        new(Policies.ReportsRead, AllowAdminWithPermission: true, AllowReception: false, AllowStaff: false),
        new(Policies.EmployeesRead, AllowAdminWithPermission: true, AllowReception: false, AllowStaff: false),
        new(Policies.EmployeesManage, AllowAdminWithPermission: true, AllowReception: false, AllowStaff: false),
        new(Policies.PackagesRead, AllowAdminWithPermission: true, AllowReception: true, AllowStaff: false),
        new(Policies.PackagesManage, AllowAdminWithPermission: true, AllowReception: false, AllowStaff: false),
        new(Policies.BranchesRead, AllowAdminWithPermission: true, AllowReception: true, AllowStaff: false),
        new(Policies.BranchesManage, AllowAdminWithPermission: true, AllowReception: false, AllowStaff: false),
        new(Policies.SettingsManage, AllowAdminWithPermission: false, AllowReception: false, AllowStaff: false),
        new(Policies.SubscriptionsRead, AllowAdminWithPermission: true, AllowReception: false, AllowStaff: false),
        new(Policies.SubscriptionsManage, AllowAdminWithPermission: false, AllowReception: false, AllowStaff: false),
        new(Policies.OnboardingManage, AllowAdminWithPermission: false, AllowReception: false, AllowStaff: false),
    };

    public static void RegisterPolicies(AuthorizationOptions options)
    {
        foreach (var rule in TenantPolicyMatrix)
        {
            options.AddPolicy(rule.PolicyName, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                {
                    var user = context.User;
                    if (user.IsInRole(Roles.SuperAdmin))
                        return true;

                    if (user.IsInRole(Roles.Admin) && rule.AllowAdminWithPermission)
                    {
                        if (user.HasClaim(c => c.Type == Policies.PermissionClaimType && c.Value == rule.PolicyName))
                            return true;
                    }

                    if (user.IsInRole(Roles.Reception) && rule.AllowReception)
                        return true;

                    if (user.IsInRole(Roles.Staff) && rule.AllowStaff)
                        return true;

                    return false;
                });
            });
        }

        options.AddPolicy(Policies.Platform.Manage, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(Roles.PlatformAdmin);
        });
    }
}
