using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Operia.Application.Common.Authorization;
using Operia.Domain.Entities;
using Operia.Infrastructure.Identity;
using System.Text.Json;

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
        await PromoteTenantOwnersAsync(dbContext, userManager);
        await SeedAdminUserAsync(userManager, logger);
        await SeedSubscriptionPlansAsync(dbContext, logger);
    }

    private static async Task PromoteTenantOwnersAsync(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        var tenantIds = await userManager.Users.Where(x => x.TenantId != null).Select(x => x.TenantId!).Distinct().ToListAsync();
        foreach (var tenantId in tenantIds)
        {
            var users = await userManager.Users.Where(x => x.TenantId == tenantId).OrderBy(x => x.Id).ToListAsync();
            var hasSuperAdmin = false;
            ApplicationUser? firstAdmin = null;
            foreach (var user in users)
            {
                if (await userManager.IsInRoleAsync(user, Roles.SuperAdmin)) hasSuperAdmin = true;
                if (firstAdmin is null && await userManager.IsInRoleAsync(user, Roles.Admin)) firstAdmin = user;
            }
            if (hasSuperAdmin) continue;
            var ownerUserId = await dbContext.Tenants
                .Where(x => x.Id == tenantId)
                .Select(x => x.OwnerUserId)
                .SingleOrDefaultAsync();
            var owner = users.FirstOrDefault(x => x.Id == ownerUserId);
            if (owner is not null && !await userManager.IsInRoleAsync(owner, Roles.Admin))
                owner = null;
            owner ??= firstAdmin;
            if (owner is null) continue;
            await userManager.RemoveFromRoleAsync(owner, Roles.Admin);
            await userManager.AddToRoleAsync(owner, Roles.SuperAdmin);
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { Roles.SuperAdmin, Roles.Admin, Roles.Reception, Roles.Staff })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        await AdminPermissionClaimHelper.AddAdminPermissionClaimsAsync(roleManager);
    }

    private static async Task SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        const string adminPhoneNumber = "+10000000000";
        const string adminEmail = "admin@operia.com";
        const string adminPassword = "Admin@12345";

        var admin = await userManager.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == adminPhoneNumber);

        if (admin is not null)
            return;

        admin = new ApplicationUser
        {
            FullName = "Operia Admin",
            UserName = adminPhoneNumber,
            Email = adminEmail,
            PhoneNumber = adminPhoneNumber,
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

        logger.LogInformation("Seeded admin user {PhoneNumber}", adminPhoneNumber);
    }

    private static async Task SeedSubscriptionPlansAsync(
        ApplicationDbContext dbContext,
        ILogger logger)
    {
        if (await dbContext.SubscriptionPlans.AnyAsync())
            return;

        var plans = new[]
        {
            new SubscriptionPlan
            {
                Name = "Free Trial",
                Code = "free-trial",
                MonthlyPrice = 0,
                YearlyPrice = 0,
                TrialDays = 14,
                FeaturesJson = JsonSerializer.Serialize(new
                             {
                                 AllFeatures = true,
                                 MaxEmployees = 3,
                                 MaxBookings = 100
                             }),
                IsActive = true
            },
            new SubscriptionPlan
            {
                Name = "Starter",
                Code = "starter",
                MonthlyPrice = 599,
                YearlyPrice = 5990,
                TrialDays = 0,
                FeaturesJson = JsonSerializer.Serialize(new
                             {
                                 AllFeatures = true,
                                 maxBranches = 1,
                                 MaxEmployees = 5,
                                 MaxBookings = "unlimited_bookings"
                             }),
                IsActive = true
            },
            new SubscriptionPlan
            {
                Name = "Growth",
                Code = "growth",
                MonthlyPrice = 1299,
                YearlyPrice = 10392,
                TrialDays = 0,
                FeaturesJson = JsonSerializer.Serialize(new
                             {
                                 AllFeatures = true,
                                 maxBranches = 3,
                                 MaxEmployees = 20,
                                 MaxBookings = "unlimited_bookings",
                                 AdvancedReports =  true
                             }),
                IsActive = true
            },
            new SubscriptionPlan
            {
                Name = "Pro",
                Code = "pro",
                MonthlyPrice = 2299,
                YearlyPrice = 18392,
                TrialDays = 0,
                FeaturesJson = JsonSerializer.Serialize(new
                             {
                                 AllFeatures = true,
                                 maxBranches = "unlimited_branches",
                                 MaxEmployees = "unlimited_employees",
                                 MaxBookings = "unlimited_bookings",
                                 AdvancedReports =  true
                             }),
                IsActive = true
            }
        };

        dbContext.SubscriptionPlans.AddRange(plans);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} subscription plans", plans.Length);
    }
}
