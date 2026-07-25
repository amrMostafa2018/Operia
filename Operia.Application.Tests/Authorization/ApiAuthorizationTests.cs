using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Operia.Application.Auth;
using Operia.Application.Common.Authorization;
using Operia.Controllers;
using Operia.Infrastructure.Authorization;
using Xunit;

namespace Operia.Application.Tests.Authorization;

public sealed class ApiAuthorizationTests
{
    public static IEnumerable<object[]> GetControllerActions()
    {
        var assembly = typeof(BranchesController).Assembly;
        var controllerTypes = assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract && t.IsPublic);

        foreach (var controller in controllerTypes)
        {
            var methods = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName);

            foreach (var method in methods)
            {
                yield return new object[] { controller.Name, method.Name };
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetControllerActions))]
    public async Task ControllerAction_HasExpectedAuthorization(string controllerName, string actionName)
    {
        var assembly = typeof(BranchesController).Assembly;
        var controllerType = assembly.GetTypes().Single(t => t.Name == controllerName);
        var method = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Single(m => m.Name == actionName);

        var methodAuth = method.GetCustomAttributes<AuthorizeAttribute>().FirstOrDefault();
        var classAuth = controllerType.GetCustomAttributes<AuthorizeAttribute>().FirstOrDefault();
        var authAttr = methodAuth ?? classAuth;
        var allowAnon = method.GetCustomAttributes<AllowAnonymousAttribute>().Any() ||
                        controllerType.GetCustomAttributes<AllowAnonymousAttribute>().Any();

        var services = new ServiceCollection();
        services.AddAuthorization(options => PolicyDefinitions.RegisterPolicies(options));
        services.AddLogging();
        services.AddOptions();
        var sp = services.BuildServiceProvider();
        var authService = sp.GetRequiredService<IAuthorizationService>();

        if (allowAnon || authAttr is null)
        {
            // Anonymous endpoint: should not require authorization
            Assert.True(allowAnon || authAttr is null, $"{controllerName}.{actionName} is anonymous");
            return;
        }

        var policyName = authAttr.Policy;
        var rolesAttr = authAttr.Roles;

        // Determine expected behaviors for each role
        var testUsers = new (string RoleName, bool HasPermissionClaim)[]
        {
            (Roles.SuperAdmin, false),
            (Roles.Admin, true),
            (Roles.Admin, false),
            (Roles.Reception, false),
            (Roles.Staff, false),
            (Roles.PlatformAdmin, false)
        };

        foreach (var (roleName, hasClaim) in testUsers)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "test-user-id"),
                new(ClaimTypes.Role, roleName)
            };

            if (hasClaim && !string.IsNullOrEmpty(policyName))
            {
                claims.Add(new Claim(Policies.PermissionClaimType, policyName));
            }

            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

            bool expectedAllowed = false;

            if (!string.IsNullOrEmpty(policyName))
            {
                if (policyName == Policies.Platform.Manage)
                {
                    expectedAllowed = (roleName == Roles.PlatformAdmin);
                }
                else
                {
                    var rule = PolicyDefinitions.TenantPolicyMatrix.SingleOrDefault(r => r.PolicyName == policyName);
                    if (rule is not null)
                    {
                        if (roleName == Roles.SuperAdmin) expectedAllowed = true;
                        else if (roleName == Roles.Admin && hasClaim && rule.AllowAdminWithPermission) expectedAllowed = true;
                        else if (roleName == Roles.Reception && rule.AllowReception) expectedAllowed = true;
                        else if (roleName == Roles.Staff && rule.AllowStaff) expectedAllowed = true;
                    }
                }

                var result = await authService.AuthorizeAsync(user, policyName);
                Assert.Equal(expectedAllowed, result.Succeeded);
            }
            else if (!string.IsNullOrEmpty(rolesAttr))
            {
                var allowedRoles = rolesAttr.Split(',').Select(r => r.Trim());
                expectedAllowed = allowedRoles.Contains(roleName);
                Assert.Equal(expectedAllowed, user.IsInRole(roleName) && expectedAllowed);
            }
            else
            {
                // [Authorize] without policy or role (e.g. GetStatus, Logout): allowed for any authenticated user
                Assert.True(user.Identity?.IsAuthenticated == true);
            }
        }
    }
}
