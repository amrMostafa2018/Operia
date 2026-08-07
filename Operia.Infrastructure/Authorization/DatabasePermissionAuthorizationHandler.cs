using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Operia.Application.Common.Authorization;

namespace Operia.Infrastructure.Authorization;

public sealed class DatabasePermissionAuthorizationHandler(
    IPermissionGrantStore permissionGrantStore) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        if (await permissionGrantStore.HasPermissionAsync(
                userId,
                requirement.Permission,
                CancellationToken.None))
        {
            context.Succeed(requirement);
        }
    }
}
