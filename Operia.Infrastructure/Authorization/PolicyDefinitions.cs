using Microsoft.AspNetCore.Authorization;
using Operia.Application.Auth;
namespace Operia.Infrastructure.Authorization;

public static class PolicyDefinitions
{
    public static void RegisterPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(Policies.AuthenticatedUser, policy =>
            policy.RequireAuthenticatedUser());

        foreach (var permission in Policies.PermissionValues)
        {
            options.AddPolicy(permission, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(permission));
            });
        }
    }
}
