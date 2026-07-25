using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Operia.Application.Auth;
using Operia.Application.Common.Authorization;
using Operia.Infrastructure.Authorization;
using Operia.Infrastructure.Identity;
using Xunit;

namespace Operia.Application.Tests.Authorization;

public sealed class PolicyDefinitionsTests
{
    [Fact]
    public void TenantPolicyMatrix_HasNoDuplicatePolicyNames()
    {
        var names = PolicyDefinitions.TenantPolicyMatrix.Select(r => r.PolicyName).ToList();
        var duplicates = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);
    }

    [Fact]
    public async Task TenantPolicyMatrix_SuperAdminReceivesEveryTenantBusinessPolicy()
    {
        var services = new ServiceCollection();
        services.AddAuthorization(options => PolicyDefinitions.RegisterPolicies(options));
        services.AddLogging();
        services.AddOptions();
        var sp = services.BuildServiceProvider();
        var authService = sp.GetRequiredService<IAuthorizationService>();

        var superAdminUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "sa-id"),
            new Claim(ClaimTypes.Role, Roles.SuperAdmin)
        }, "TestAuth"));

        foreach (var rule in PolicyDefinitions.TenantPolicyMatrix)
        {
            var result = await authService.AuthorizeAsync(superAdminUser, rule.PolicyName);
            Assert.True(result.Succeeded, $"Super Admin was denied policy {rule.PolicyName}");
        }
    }

    [Fact]
    public async Task TenantPolicyMatrix_AdminWithoutClaim_IsDenied()
    {
        var services = new ServiceCollection();
        services.AddAuthorization(options => PolicyDefinitions.RegisterPolicies(options));
        services.AddLogging();
        services.AddOptions();
        var sp = services.BuildServiceProvider();
        var authService = sp.GetRequiredService<IAuthorizationService>();

        var adminUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-id"),
            new Claim(ClaimTypes.Role, Roles.Admin)
        }, "TestAuth"));

        foreach (var rule in PolicyDefinitions.TenantPolicyMatrix)
        {
            var result = await authService.AuthorizeAsync(adminUser, rule.PolicyName);
            Assert.False(result.Succeeded, $"Admin without claim was allowed policy {rule.PolicyName}");
        }
    }

    [Fact]
    public async Task TenantPolicyMatrix_AdminWithClaim_IsAllowedOnlyForEligiblePolicies()
    {
        var services = new ServiceCollection();
        services.AddAuthorization(options => PolicyDefinitions.RegisterPolicies(options));
        services.AddLogging();
        services.AddOptions();
        var sp = services.BuildServiceProvider();
        var authService = sp.GetRequiredService<IAuthorizationService>();

        foreach (var rule in PolicyDefinitions.TenantPolicyMatrix)
        {
            var adminUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "admin-id"),
                new Claim(ClaimTypes.Role, Roles.Admin),
                new Claim(Policies.PermissionClaimType, rule.PolicyName)
            }, "TestAuth"));

            var result = await authService.AuthorizeAsync(adminUser, rule.PolicyName);
            if (rule.AllowAdminWithPermission)
            {
                Assert.True(result.Succeeded, $"Eligible Admin with claim was denied policy {rule.PolicyName}");
            }
            else
            {
                Assert.False(result.Succeeded, $"Ineligible Admin with claim was allowed policy {rule.PolicyName}");
            }
        }
    }

    [Fact]
    public void AdminPermissionClaimHelper_ReturnsExactly15EligibleDefaultPolicies()
    {
        var claims = AdminPermissionClaimHelper.GetAdminPermissionClaims().ToList();
        Assert.Equal(15, claims.Count);
        Assert.All(claims, c => Assert.Equal(Policies.PermissionClaimType, c.Type));

        var ineligible = new[] { Policies.SettingsManage, Policies.SubscriptionsManage, Policies.OnboardingManage };
        foreach (var inel in ineligible)
        {
            Assert.DoesNotContain(claims, c => c.Value == inel);
        }
    }

    [Fact]
    public async Task PlatformManage_RequiresPlatformAdminRole()
    {
        var services = new ServiceCollection();
        services.AddAuthorization(options => PolicyDefinitions.RegisterPolicies(options));
        services.AddLogging();
        services.AddOptions();
        var sp = services.BuildServiceProvider();
        var authService = sp.GetRequiredService<IAuthorizationService>();

        var platformAdmin = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "pa-id"),
            new Claim(ClaimTypes.Role, Roles.PlatformAdmin)
        }, "TestAuth"));

        var result = await authService.AuthorizeAsync(platformAdmin, Policies.Platform.Manage);
        Assert.True(result.Succeeded);

        foreach (var role in new[] { Roles.SuperAdmin, Roles.Admin, Roles.Reception, Roles.Staff })
        {
            var tenantUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "t-id"),
                new Claim(ClaimTypes.Role, role)
            }, "TestAuth"));

            var tenantResult = await authService.AuthorizeAsync(tenantUser, Policies.Platform.Manage);
            Assert.False(tenantResult.Succeeded, $"Role {role} was incorrectly allowed PlatformOperations.Manage");
        }
    }
}
