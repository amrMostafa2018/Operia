using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Operia.Application.Auth;
using Operia.Application.Common.Authorization;
using Operia.Domain.Common;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Interfaces;
using Operia.Infrastructure.Identity;

namespace Operia.Infrastructure.Persistence;

/// <summary>Restores the US-07 prerequisites in reserved local development tenants.</summary>
public static class E2EDataSeeder
{
    private const string TenantA = "e2e70000-0000-4000-8000-000000000001";
    private const string UserA = "e2e70000-0000-4000-8000-000000000002";
    private const string BusinessA = "e2e70000-0000-4000-8000-000000000003";
    private const string BranchA = "e2e70000-0000-4000-8000-000000000004";
    private const string EmployeeA = "e2e70000-0000-4000-8000-000000000005";
    private const string CustomerA = "e2e70000-0000-4000-8000-000000000006";
    private const string CategoryA = "e2e70000-0000-4000-8000-000000000017";
    private const string TenantB = "e2e70000-0000-4000-8000-000000000101";
    private const string UserB = "e2e70000-0000-4000-8000-000000000102";
    private const string BusinessB = "e2e70000-0000-4000-8000-000000000103";
    private const string NameA = "[E2E US-07] Tenant A";
    private const string NameB = "[E2E US-07] Tenant B";

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();

        AssertLocalDevelopmentDatabase(db);
        var password = config["E2E:Password"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("E2E:Password is required for the test users.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await using var transaction = await db.Database.BeginTransactionAsync();

        await SeedUserAsync(users, UserA, TenantA, "+201000000071", password);
        await SeedUserAsync(users, UserB, TenantB, "+201000000072", password);
        await SeedTenantAsync(db, TenantA, UserA, NameA);
        await SeedTenantAsync(db, TenantB, UserB, NameB);
        await UpsertAsync(db, new Business
        {
            Id = BusinessA,
            TenantId = TenantA,
            ActivityName = NameA,
            Status = BusinessStatus.Active
        });
        await UpsertAsync(db, new Business
        {
            Id = BusinessB,
            TenantId = TenantB,
            ActivityName = NameB,
            Status = BusinessStatus.Active
        });
        var planId = await db.SubscriptionPlans.AsNoTracking()
            .Where(x => x.Code == "pro").Select(x => x.Id).SingleAsync();
        await SeedSubscriptionAsync(db, TenantA, planId, today,
            "e2e70000-0000-4000-8000-000000000007");
        await SeedSubscriptionAsync(db, TenantB, planId, today,
            "e2e70000-0000-4000-8000-000000000104");
        await UpsertAsync(db, new BusinessSettings
        {
            Id = "e2e70000-0000-4000-8000-000000000008",
            BusinessId = BusinessA,
            PaymentMethodsJson = "{}",
            WorkingDaysJson = "[]"
        });
        await UpsertAsync(db, new Branch
        {
            Id = BranchA,
            TenantId = TenantA,
            BusinessId = BusinessA,
            Name = "US-07 E2E Branch",
            Address = "Test address",
            PhoneNumber = "+201000000071",
            GoogleMapsUrl = string.Empty
        });
        await UpsertAsync(db, new Employee
        {
            Id = EmployeeA,
            TenantId = TenantA,
            BusinessId = BusinessA,
            IdentityUserId = UserA,
            Code = "E2E07",
            FullName = "US-07 Test Employee",
            Email = "e2e-us07-employee@operia.example",
            MobileNumber = "+201000000073",
            JoiningDate = today.AddDays(-30),
            IsActive = true
        });
        await UpsertAsync(db, new UserBranch
        {
            Id = "e2e70000-0000-4000-8000-000000000009",
            TenantId = TenantA,
            EmployeeId = EmployeeA,
            BranchId = BranchA
        });
        await UpsertAsync(db, new Customer
        {
            Id = CustomerA,
            TenantId = TenantA,
            FullName = "US-07 Test Customer",
            MobileNumber = "+201000000074",
            NormalizedMobileNumber = "+201000000074",
            IsActive = true
        });
        var days = new[] { "sun", "mon", "tue", "wed", "thu", "fri", "sat" };
        for (var index = 0; index < days.Length; index++)
        {
            await UpsertAsync(db, new EmployeeWorkingDay
            {
                Id = $"e2e70000-0000-4000-8000-{index + 10:000000000000}",
                TenantId = TenantA,
                EmployeeId = EmployeeA,
                BranchId = BranchA,
                Day = days[index],
                Enabled = true,
                FromTime = new TimeOnly(8, 0),
                ToTime = new TimeOnly(18, 0)
            });
        }
        await db.SaveChangesAsync();

        // Only the reserved E2E tenants are cleared. Every test-created offer,
        // booking and purchase starts fresh on the next Playwright run.
        var tenantIds = new[] { TenantA, TenantB };
        var userIds = new[] { UserA, UserB };
        await db.RefreshTokens.Where(x => userIds.Contains(x.UserId))
            .ExecuteDeleteAsync();
        await db.BookingPackageReservations.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.CustomerPackageInstallments.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.BookingItems.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.BookingHistory.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.BookingHolds.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.CustomerPackages.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.Bookings.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.PackageInstallments.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.Packages.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.SubServiceCategories.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.ServiceCategories.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.AuditLogs.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();

        db.ServiceCategories.Add(new ServiceCategory
        {
            Id = CategoryA,
            TenantId = TenantA,
            BusinessId = BusinessA,
            Name = "US-07 E2E Category",
            Icon = "pi pi-box"
        });
        await db.SaveChangesAsync();

        await transaction.CommitAsync();
        logger.LogInformation("Restored US-07 E2E prerequisites in the local development database");
    }

    private static void AssertLocalDevelopmentDatabase(ApplicationDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (!connection.DataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase) ||
            connection.Database is not ("Operia_Local" or "Operia_Dev"))
        {
            throw new InvalidOperationException(
                "E2E seeding is restricted to a local Operia development database.");
        }
    }

    private static async Task SeedUserAsync(UserManager<ApplicationUser> users,
        string userId, string tenantId, string phone, string password)
    {
        var user = await users.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            if (await users.Users.AnyAsync(x => x.PhoneNumber == phone))
                throw new InvalidOperationException("The reserved E2E phone is already in use.");
            user = new ApplicationUser
            {
                Id = userId,
                UserName = phone,
                PhoneNumber = phone,
                Email = $"{userId}@operia.example",
                FullName = "US-07 E2E Owner",
                TenantId = tenantId,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true
            };
            RequireSuccess(await users.CreateAsync(user, password), "create E2E user");
        }
        else
        {
            if (user.TenantId != tenantId || user.PhoneNumber != phone)
                throw new InvalidOperationException("The reserved E2E user ID is in use.");
            var token = await users.GeneratePasswordResetTokenAsync(user);
            RequireSuccess(await users.ResetPasswordAsync(user, token, password),
                "reset E2E password");
        }

        if (!await users.IsInRoleAsync(user, Roles.SuperAdmin))
            RequireSuccess(await users.AddToRoleAsync(user, Roles.SuperAdmin),
                "grant E2E owner role");
        var existingClaims = await users.GetClaimsAsync(user);
        foreach (var permission in Policies.TenantPermissionValues)
        {
            if (existingClaims.Any(x => x.Type == Policies.PermissionClaimType &&
                x.Value == permission))
                continue;
            RequireSuccess(await users.AddClaimAsync(user,
                new Claim(Policies.PermissionClaimType, permission)),
                $"grant E2E permission {permission}");
        }
    }

    private static async Task SeedTenantAsync(ApplicationDbContext db,
        string tenantId, string userId, string name)
    {
        var existing = await db.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId);
        if (existing is not null && existing.BusinessName != name)
            throw new InvalidOperationException("The reserved E2E tenant ID is in use.");
        await UpsertAsync(db, new Tenant
        {
            Id = tenantId,
            OwnerUserId = userId,
            BusinessName = name,
            BusinessType = BusinessType.LaserClinic,
            CountryCode = "EG",
            City = "Cairo",
            CurrencyCode = "EGP",
            Timezone = "Africa/Cairo",
            Status = TenantStatus.Active
        });
    }

    private static Task SeedSubscriptionAsync(ApplicationDbContext db,
        string tenantId, string planId, DateOnly today, string id) =>
        UpsertAsync(db, new TenantSubscription
        {
            Id = id,
            TenantId = tenantId,
            PlanId = planId,
            Amount = 0,
            Currency = "EGP",
            BillingType = BillingType.Monthly,
            StartDate = today.AddDays(-1),
            EndDate = today.AddYears(1),
            Status = SubscriptionStatus.Active
        });

    private static async Task UpsertAsync<TEntity>(ApplicationDbContext db, TEntity value)
        where TEntity : Entity
    {
        var id = (string)db.Entry(value).Property("Id").CurrentValue!;
        var existing = await db.Set<TEntity>().IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => EF.Property<string>(x, "Id") == id);
        if (existing is null)
        {
            db.Set<TEntity>().Add(value);
            return;
        }
        if (existing is ITenantScoped scoped &&
            scoped.TenantId is not (TenantA or TenantB))
            throw new InvalidOperationException($"Reserved E2E ID {id} is in use.");
        var entry = db.Entry(existing);
        entry.CurrentValues.SetValues(value);
        foreach (var property in entry.Properties.Where(x => x.Metadata.IsConcurrencyToken))
            property.IsModified = false;
    }

    private static void RequireSuccess(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not {operation}: " +
                string.Join(", ", result.Errors.Select(x => x.Description)));
    }
}
