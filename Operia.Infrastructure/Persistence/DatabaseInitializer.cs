using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Operia.Application.Common.Authorization;
using Operia.Infrastructure.Identity;

namespace Operia.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();

        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await SeedRolesAsync(roleManager);
        await SeedAdminUserAsync(userManager, logger);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { Roles.Admin, Roles.Staff })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private static async Task SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        const string adminEmail = "admin@operia.com";
        const string adminPassword = "Admin@12345";

        var admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin is not null)
        {
            if (await userManager.IsInRoleAsync(admin, Roles.Admin))
                await EnsurePermissionClaimsAsync(userManager, admin, Roles.Admin);

            return;
        }

        admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            PhoneNumber = "+10000000000",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, adminPassword);

        if (!result.Succeeded)
        {
            logger.LogWarning("Failed to seed admin user: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, Roles.Admin);
        await AdminPermissionClaimHelper.AddAdminPermissionClaimsAsync(userManager, admin);

        logger.LogInformation("Seeded admin user {Email}", adminEmail);
    }

    private static async Task EnsurePermissionClaimsAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string role)
    {
        var existing = await userManager.GetClaimsAsync(user);
        if (existing.Any(c => c.Type == Permissions.ClaimType))
            return;

        if (role == Roles.Admin)
        {
            await AdminPermissionClaimHelper.AddAdminPermissionClaimsAsync(userManager, user);
            return;
        }

        if (role == Roles.Staff)
        {
            await userManager.AddClaimAsync(user, new Claim(Permissions.ClaimType, Permissions.Staff.Read));
            await userManager.AddClaimAsync(user, new Claim(Permissions.ClaimType, Permissions.Staff.Write));
        }
    }
}
