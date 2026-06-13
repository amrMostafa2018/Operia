using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Operia.Infrastructure.Identity;

namespace Operia.Infrastructure.Middleware;

public sealed class SecurityStampValidationMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityStampValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var stampClaim = context.User.FindFirstValue("security_stamp");

            if (userId is null || stampClaim is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var user = await userManager.FindByIdAsync(userId);

            if (user is null || user.SecurityStamp != stampClaim)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await _next(context);
    }
}
