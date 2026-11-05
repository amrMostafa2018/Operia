using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Operia.Application.Auth;
using Operia.Application.Common.Authorization;
using Operia.Application.Packages;
using Operia.Application.Sales.Commands.ConfirmSale;
using Operia.Domain.Common;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Interfaces;
using Operia.Infrastructure.Identity;

namespace Operia.Infrastructure.Persistence;

/// <summary>Restores the US-07 through US-15 prerequisites in reserved local development tenants.</summary>
public static class E2EDataSeeder
{
    private const string TenantA = "e2e70000-0000-4000-8000-000000000001";
    private const string UserA = "e2e70000-0000-4000-8000-000000000002";
    private const string BusinessA = "e2e70000-0000-4000-8000-000000000003";
    private const string BranchA = "e2e70000-0000-4000-8000-000000000004";
    private const string EmployeeA = "e2e70000-0000-4000-8000-000000000005";
    private const string CustomerA = "e2e70000-0000-4000-8000-000000000006";
    private const string CategoryA = "e2e70000-0000-4000-8000-000000000017";
    private const string StaffUserA = "e2e70000-0000-4000-8000-000000000018";
    private const string StaffEmployeeA = "e2e70000-0000-4000-8000-000000000019";
    private const string AllStaffOfferA = "e2e70000-0000-4000-8000-000000000030";
    private const string SelectedStaffOfferA = "e2e70000-0000-4000-8000-000000000031";
    private const string PulseOfferA = "e2e70000-0000-4000-8000-000000000032";
    private const string InquiryPurchaseA = "e2e70000-0000-4000-8000-000000000040";
    private const string InquiryPurchaseB = "e2e70000-0000-4000-8000-000000000041";
    private const string DetailPurchaseA = "e2e70000-0000-4000-8000-000000000042";
    private const string PulsePurchaseA = "e2e70000-0000-4000-8000-000000000043";
    private const string CompletedBookingA = "e2e70000-0000-4000-8000-000000000060";
    private const string CompletedBookingItemA = "e2e70000-0000-4000-8000-000000000061";
    private const string CashServiceOfferA = "e2e70000-0000-4000-8000-000000000070";
    private const string SaleInstallmentOfferA = "e2e70000-0000-4000-8000-000000000071";
    private const string StaleSaleOfferA = "e2e70000-0000-4000-8000-000000000072";
    private const string OwnedSessionOfferA = "e2e70000-0000-4000-8000-000000000073";
    private const string CollectionOfferA = "e2e70000-0000-4000-8000-000000000074";
    private const string DirectServiceOfferA = "e2e70000-0000-4000-8000-000000000089";
    private const string BookingServiceOfferA = "e2e70000-0000-4000-8000-000000000090";
    private const string PendingSuccessSaleA = "e2e70000-0000-4000-8000-000000000091";
    private const string PendingFailureSaleA = "e2e70000-0000-4000-8000-000000000092";
    private const string OwnedSessionPurchaseA = "e2e70000-0000-4000-8000-000000000081";
    private const string UiCollectionPurchaseA = "e2e70000-0000-4000-8000-000000000082";
    private const string ApiCollectionPurchaseA = "e2e70000-0000-4000-8000-000000000083";
    private const string LockedCollectionPurchaseA = "e2e70000-0000-4000-8000-000000000084";
    private const string FinalCollectionPurchaseA = "e2e70000-0000-4000-8000-000000000085";
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

        await SeedUserAsync(
            users,
            UserA,
            TenantA,
            "+201000000071",
            "US-07 E2E Owner",
            Roles.SuperAdmin,
            password,
            grantAllTenantPermissions: true);
        await SeedUserAsync(
            users,
            UserB,
            TenantB,
            "+201000000072",
            "US-07 E2E Tenant B Owner",
            Roles.SuperAdmin,
            password,
            grantAllTenantPermissions: true);
        await SeedUserAsync(
            users,
            StaffUserA,
            TenantA,
            "+201000000075",
            "US-08 E2E Staff User",
            Roles.Staff,
            password,
            grantAllTenantPermissions: false);
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
        await UpsertAsync(db, new Employee
        {
            Id = StaffEmployeeA,
            TenantId = TenantA,
            BusinessId = BusinessA,
            IdentityUserId = StaffUserA,
            Code = "E2E08",
            FullName = "US-08 Test Staff",
            Email = "e2e-us08-staff@operia.example",
            MobileNumber = "+201000000075",
            JoiningDate = today.AddDays(-20),
            IsActive = true
        });
        await UpsertAsync(db, new UserBranch
        {
            Id = "e2e70000-0000-4000-8000-000000000009",
            TenantId = TenantA,
            EmployeeId = EmployeeA,
            BranchId = BranchA
        });
        await UpsertAsync(db, new UserBranch
        {
            Id = "e2e70000-0000-4000-8000-000000000020",
            TenantId = TenantA,
            EmployeeId = StaffEmployeeA,
            BranchId = BranchA
        });
        await UpsertAsync(db, new Customer
        {
            Id = CustomerA,
            TenantId = TenantA,
            FullName = "US-07 Test Customer",
            MobileNumber = "+201000000074",
            NormalizedMobileNumber = "201000000074",
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
            await UpsertAsync(db, new EmployeeWorkingDay
            {
                Id = $"e2e70000-0000-4000-8000-{index + 21:000000000000}",
                TenantId = TenantA,
                EmployeeId = StaffEmployeeA,
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
        var userIds = new[] { UserA, UserB, StaffUserA };
        await db.RefreshTokens.Where(x => userIds.Contains(x.UserId))
            .ExecuteDeleteAsync();
        await db.BookingPackageReservations.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.SaleCollections.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.InstallmentCollections.IgnoreQueryFilters()
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
        await db.Sales.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.Customers.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId) && x.Id != CustomerA)
            .ExecuteDeleteAsync();
        await db.PackageInstallments.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.PackageStaff.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.Packages.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.SubServiceCategories.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.ServiceCategories.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();
        await db.AuditLogs.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId)).ExecuteDeleteAsync();

        var category = new ServiceCategory
        {
            Id = CategoryA,
            TenantId = TenantA,
            BusinessId = BusinessA,
            Name = "US-07 to US-12 E2E Category",
            Icon = "pi pi-box"
        };
        db.ServiceCategories.Add(category);

        var allStaffOffer = new Package
        {
            Id = AllStaffOfferA,
            TenantId = TenantA,
            BusinessId = BusinessA,
            Name = "E2E Inquiry Package",
            Description = "All Staff offer used by the US-09 inquiry fixtures.",
            OfferType = OfferType.Package,
            ServiceCategoryId = CategoryA,
            SessionDurationMinutes = 30,
            SessionCount = 4,
            PackageExpiryMonths = 12,
            Price = 1200,
            CashEnabled = true,
            InstallmentEnabled = false,
            AllStaff = true,
            Status = PackageStatus.Active
        };
        db.Packages.Add(allStaffOffer);

        var selectedStaffOffer = new Package
        {
            Id = SelectedStaffOfferA,
            TenantId = TenantA,
            BusinessId = BusinessA,
            Name = "E2E Saved Installment Package",
            Description = "Selected Staff offer used by the US-10 detail fixture.",
            OfferType = OfferType.Package,
            ServiceCategoryId = CategoryA,
            SessionDurationMinutes = 30,
            SessionCount = 10,
            PackageExpiryMonths = 12,
            Price = 1800,
            CashEnabled = true,
            InstallmentEnabled = true,
            InstallmentPrice = 2000,
            FirstPayment = 500,
            InstallmentCount = 3,
            AllStaff = false,
            Status = PackageStatus.Active,
            Installments =
            [
                NewOfferInstallment("e2e70000-0000-4000-8000-000000000034",
                    SelectedStaffOfferA, 1, 500, 3, 3),
                NewOfferInstallment("e2e70000-0000-4000-8000-000000000035",
                    SelectedStaffOfferA, 2, 500, 3, 6),
                NewOfferInstallment("e2e70000-0000-4000-8000-000000000036",
                    SelectedStaffOfferA, 3, 500, 4, 10)
            ],
            AvailableStaff =
            [
                new PackageStaff
                {
                    Id = "e2e70000-0000-4000-8000-000000000033",
                    TenantId = TenantA,
                    PackageId = SelectedStaffOfferA,
                    EmployeeId = StaffEmployeeA
                }
            ]
        };
        db.Packages.Add(selectedStaffOffer);

        var pulseOffer = new Package
        {
            Id = PulseOfferA,
            TenantId = TenantA,
            BusinessId = BusinessA,
            Name = "E2E Pulse Package",
            Description = "Pulse balance fixture for US-10.",
            OfferType = OfferType.Package,
            ServiceCategoryId = CategoryA,
            SessionDurationMinutes = 30,
            PulseCount = 8000,
            PackageExpiryMonths = 12,
            Price = 800,
            CashEnabled = true,
            InstallmentEnabled = false,
            AllStaff = true,
            Status = PackageStatus.Active
        };
        db.Packages.Add(pulseOffer);

        var cashServiceOffer = new Package
        {
            Id = CashServiceOfferA,
            TenantId = TenantA,
            BusinessId = BusinessA,
            Name = "E2E Sale Cash Service",
            Description = "Cash service for US-12 sale composition.",
            OfferType = OfferType.SingleSession,
            ServiceCategoryId = CategoryA,
            SessionDurationMinutes = 45,
            Price = 300,
            CashEnabled = true,
            InstallmentEnabled = false,
            AllStaff = true,
            Status = PackageStatus.Active
        };
        db.Packages.Add(cashServiceOffer);

        db.Packages.AddRange(
            new Package
            {
                Id = DirectServiceOfferA,
                TenantId = TenantA,
                BusinessId = BusinessA,
                Name = "E2E Direct Service",
                OfferType = OfferType.SingleSession,
                ServiceCategoryId = CategoryA,
                SessionDurationMinutes = 30,
                Price = 350,
                CashEnabled = true,
                InstallmentEnabled = false,
                AllStaff = true,
                Status = PackageStatus.Active
            },
            new Package
            {
                Id = BookingServiceOfferA,
                TenantId = TenantA,
                BusinessId = BusinessA,
                Name = "E2E Booking Service",
                OfferType = OfferType.SingleSession,
                ServiceCategoryId = CategoryA,
                SessionDurationMinutes = 30,
                Price = 0,
                CashEnabled = true,
                InstallmentEnabled = false,
                AllStaff = true,
                Status = PackageStatus.Active
            });

        var saleInstallmentOffer = new Package
        {
            Id = SaleInstallmentOfferA,
            TenantId = TenantA,
            BusinessId = BusinessA,
            Name = "E2E Sale Installment Package",
            Description = "Installment package for US-12 sale composition.",
            OfferType = OfferType.Package,
            ServiceCategoryId = CategoryA,
            SessionDurationMinutes = 30,
            SessionCount = 10,
            PackageExpiryMonths = 12,
            Price = 1800,
            CashEnabled = true,
            InstallmentEnabled = true,
            InstallmentPrice = 2000,
            FirstPayment = 500,
            InstallmentCount = 3,
            AllStaff = true,
            Status = PackageStatus.Active,
            Installments =
            [
                NewOfferInstallment("e2e70000-0000-4000-8000-000000000075",
                    SaleInstallmentOfferA, 1, 500, 3, 3),
                NewOfferInstallment("e2e70000-0000-4000-8000-000000000076",
                    SaleInstallmentOfferA, 2, 500, 3, 6),
                NewOfferInstallment("e2e70000-0000-4000-8000-000000000077",
                    SaleInstallmentOfferA, 3, 500, 4, 10)
            ]
        };
        db.Packages.Add(saleInstallmentOffer);

        var pendingItems = JsonSerializer.Serialize(new[]
        {
            new ConfirmedSaleItemSnapshot(
                SaleInstallmentOfferA, null, saleInstallmentOffer.Name, "package",
                30, 10, 1, "installment", 2000, 0, 2000, 500,
                PackagePurchaseSnapshot.Capture(saleInstallmentOffer, "installment", 2000))
        });
        db.Sales.AddRange(
            new Sale
            {
                Id = PendingSuccessSaleA,
                TenantId = TenantA,
                BranchId = BranchA,
                CustomerId = CustomerA,
                IdempotencyKey = "e2e-us14-success-prerequisite",
                RequestHash = "seeded-us14-success",
                PaymentMethod = "cash",
                GrandTotal = 2000,
                DueNow = 500,
                ItemsSnapshotJson = pendingItems,
                Disposition = "Close",
                Status = "PendingCollection"
            },
            new Sale
            {
                Id = PendingFailureSaleA,
                TenantId = TenantA,
                BranchId = BranchA,
                CustomerId = CustomerA,
                IdempotencyKey = "e2e-us14-failure-prerequisite",
                RequestHash = "seeded-us14-failure",
                PaymentMethod = "cash",
                GrandTotal = 2000,
                DueNow = 500,
                ItemsSnapshotJson = pendingItems,
                Disposition = "Later",
                Status = "PendingCollection"
            });

        var staleSaleOffer = new Package
        {
            Id = StaleSaleOfferA,
            TenantId = TenantA,
            BusinessId = BusinessA,
            Name = "E2E Sale Stale Draft Service",
            Description = "Dedicated offer disabled after a US-12 quote.",
            OfferType = OfferType.SingleSession,
            ServiceCategoryId = CategoryA,
            SessionDurationMinutes = 20,
            Price = 225,
            CashEnabled = true,
            InstallmentEnabled = false,
            AllStaff = true,
            Status = PackageStatus.Active
        };
        db.Packages.Add(staleSaleOffer);

        var ownedSessionOffer = new Package
        {
            Id = OwnedSessionOfferA,
            TenantId = TenantA,
            BusinessId = BusinessA,
            Name = "E2E Owned Session Package",
            Description = "Owned-session choice for the US-12 sale builder.",
            OfferType = OfferType.Package,
            ServiceCategoryId = CategoryA,
            SessionDurationMinutes = 40,
            SessionCount = 4,
            PackageExpiryMonths = 12,
            Price = 1000,
            CashEnabled = true,
            InstallmentEnabled = false,
            AllStaff = true,
            Status = PackageStatus.Active
        };
        db.Packages.Add(ownedSessionOffer);

        var collectionOffer = new Package
        {
            Id = CollectionOfferA,
            TenantId = TenantA,
            BusinessId = BusinessA,
            Name = "E2E US-11 Collection Package",
            Description = "Saved-plan source for independent US-11 purchase states.",
            OfferType = OfferType.Package,
            ServiceCategoryId = CategoryA,
            SessionDurationMinutes = 30,
            SessionCount = 10,
            PackageExpiryMonths = 12,
            Price = 1800,
            CashEnabled = true,
            InstallmentEnabled = true,
            InstallmentPrice = 2000,
            FirstPayment = 500,
            InstallmentCount = 3,
            AllStaff = true,
            Status = PackageStatus.Active,
            Installments =
            [
                NewOfferInstallment("e2e70000-0000-4000-8000-000000000078",
                    CollectionOfferA, 1, 500, 3, 3),
                NewOfferInstallment("e2e70000-0000-4000-8000-000000000079",
                    CollectionOfferA, 2, 500, 3, 6),
                NewOfferInstallment("e2e70000-0000-4000-8000-000000000080",
                    CollectionOfferA, 3, 500, 4, 10)
            ]
        };
        db.Packages.Add(collectionOffer);

        db.CustomerPackages.AddRange(
            NewPurchase(
                InquiryPurchaseA,
                AllStaffOfferA,
                PackagePurchaseSnapshot.Capture(allStaffOffer, "cash", 1200),
                1200,
                0,
                4,
                0,
                isActive: true,
                today.AddMonths(12)),
            NewPurchase(
                InquiryPurchaseB,
                AllStaffOfferA,
                PackagePurchaseSnapshot.Capture(allStaffOffer, "cash", 1200),
                1200,
                0,
                4,
                0,
                isActive: false,
                today.AddMonths(12)),
            NewPurchase(
                DetailPurchaseA,
                SelectedStaffOfferA,
                PackagePurchaseSnapshot.Capture(selectedStaffOffer, "installment", 2000),
                2000,
                1500,
                3,
                1,
                isActive: true,
                today.AddMonths(12)),
            NewPurchase(
                PulsePurchaseA,
                PulseOfferA,
                PackagePurchaseSnapshot.Capture(pulseOffer, "cash", 800),
                800,
                0,
                8000,
                2500,
                isActive: true,
                today.AddMonths(12)),
            NewPurchase(
                OwnedSessionPurchaseA,
                OwnedSessionOfferA,
                PackagePurchaseSnapshot.Capture(ownedSessionOffer, "cash", 1000),
                1000,
                0,
                4,
                1,
                isActive: true,
                today.AddMonths(12),
                releasedSessions: 4),
            NewPurchase(
                UiCollectionPurchaseA,
                CollectionOfferA,
                PackagePurchaseSnapshot.Capture(collectionOffer, "installment", 2000) with
                {
                    OfferName = "E2E Collect in UI"
                },
                2000,
                1500,
                10,
                0,
                isActive: true,
                today.AddMonths(12),
                releasedSessions: 0),
            NewPurchase(
                ApiCollectionPurchaseA,
                CollectionOfferA,
                PackagePurchaseSnapshot.Capture(collectionOffer, "installment", 2000) with
                {
                    OfferName = "E2E Collect Idempotently"
                },
                2000,
                1500,
                10,
                0,
                isActive: true,
                today.AddMonths(12),
                releasedSessions: 0),
            NewPurchase(
                LockedCollectionPurchaseA,
                CollectionOfferA,
                PackagePurchaseSnapshot.Capture(collectionOffer, "installment", 2000) with
                {
                    OfferName = "E2E Progress Locked"
                },
                2000,
                1000,
                10,
                2,
                isActive: true,
                today.AddMonths(12),
                releasedSessions: 3),
            NewPurchase(
                FinalCollectionPurchaseA,
                CollectionOfferA,
                PackagePurchaseSnapshot.Capture(collectionOffer, "installment", 2000) with
                {
                    OfferName = "E2E Final Installment"
                },
                2000,
                500,
                10,
                6,
                isActive: true,
                today.AddMonths(12),
                releasedSessions: 6));

        db.CustomerPackageInstallments.AddRange(
            NewPurchaseInstallment("e2e70000-0000-4000-8000-000000000050", 0, 500, 0, DateTime.UtcNow),
            NewPurchaseInstallment("e2e70000-0000-4000-8000-000000000051", 1, 500, 3),
            NewPurchaseInstallment("e2e70000-0000-4000-8000-000000000052", 2, 500, 6),
            NewPurchaseInstallment("e2e70000-0000-4000-8000-000000000053", 3, 500, 10));

        db.InstallmentCollections.AddRange(
            NewCollection("e2e70000-0000-4000-8000-000000000086",
                LockedCollectionPurchaseA, 1, "e2e-us11-locked-prerequisite"),
            NewCollection("e2e70000-0000-4000-8000-000000000087",
                FinalCollectionPurchaseA, 1, "e2e-us11-final-prerequisite-1"),
            NewCollection("e2e70000-0000-4000-8000-000000000088",
                FinalCollectionPurchaseA, 2, "e2e-us11-final-prerequisite-2"));

        var completedBooking = new Booking
        {
            Id = CompletedBookingA,
            TenantId = TenantA,
            BookingNumber = "E2E-US10-0001",
            IdempotencyKey = "e2e-us10-completed-booking",
            BranchId = BranchA,
            EmployeeId = StaffEmployeeA,
            CustomerId = CustomerA,
            CustomerName = "US-07 Test Customer",
            CustomerMobile = "+201000000074",
            ScheduledDate = today.AddDays(-7),
            StartMinutes = 600,
            EndMinutes = 630,
            Status = BookingStatus.Completed,
            Source = "AdminPortal",
            PaymentMethod = "cash",
            TotalAmount = 0,
            PaidAmount = 0,
            DiscountAmount = 0
        };
        db.Bookings.Add(completedBooking);
        db.BookingItems.Add(new BookingItem
        {
            Id = CompletedBookingItemA,
            TenantId = TenantA,
            BookingId = CompletedBookingA,
            Type = BookingItemType.PackageSession,
            PackageId = SelectedStaffOfferA,
            Name = "E2E Saved Installment Package",
            Quantity = 1,
            DurationMinutes = 30,
            UnitPrice = 0,
            PaymentOption = "installment"
        });
        db.BookingPackageReservations.Add(new BookingPackageReservation
        {
            Id = "e2e70000-0000-4000-8000-000000000062",
            TenantId = TenantA,
            BookingId = CompletedBookingA,
            CustomerPackageId = DetailPurchaseA,
            SessionNumber = 1
        });
        db.BookingHistory.Add(new BookingHistory
        {
            Id = "e2e70000-0000-4000-8000-000000000063",
            TenantId = TenantA,
            BookingId = CompletedBookingA,
            Action = "Closed",
            ChangedByUserId = UserA,
            ChangedByDisplayName = "US-07 E2E Owner",
            ChangesJson = $$"""
                {"Items":[{"BookingItemId":"{{CompletedBookingItemA}}","Name":"E2E Saved Installment Package","Status":"complete","PulsesUsed":null,"Notes":"US-10 seeded execution","CustomerPackageIds":["{{DetailPurchaseA}}"]}]}
                """
        });
        await db.SaveChangesAsync();

        await transaction.CommitAsync();
        logger.LogInformation(
            "Restored US-07 through US-15 E2E prerequisites in the local development database");
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

    private static async Task SeedUserAsync(
        UserManager<ApplicationUser> users,
        string userId,
        string tenantId,
        string phone,
        string fullName,
        string role,
        string password,
        bool grantAllTenantPermissions)
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
                FullName = fullName,
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

        var existingRoles = await users.GetRolesAsync(user);
        foreach (var existingRole in existingRoles.Where(existingRole => existingRole != role))
        {
            RequireSuccess(await users.RemoveFromRoleAsync(user, existingRole),
                $"remove E2E role {existingRole}");
        }
        if (!await users.IsInRoleAsync(user, role))
        {
            RequireSuccess(await users.AddToRoleAsync(user, role), $"grant E2E role {role}");
        }
        var existingClaims = await users.GetClaimsAsync(user);
        if (!grantAllTenantPermissions)
        {
            foreach (var claim in existingClaims.Where(claim =>
                         claim.Type == Policies.PermissionClaimType))
            {
                RequireSuccess(await users.RemoveClaimAsync(user, claim),
                    $"remove E2E permission {claim.Value}");
            }
            return;
        }
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

    private static PackageInstallment NewOfferInstallment(
        string id,
        string packageId,
        int number,
        decimal amount,
        int sessionsUnlocked,
        int cumulativeAvailableSessions) =>
        new()
        {
            Id = id,
            TenantId = TenantA,
            PackageId = packageId,
            Number = number,
            Amount = amount,
            SessionsUnlocked = sessionsUnlocked,
            CumulativeAvailableSessions = cumulativeAvailableSessions
        };

    private static CustomerPackage NewPurchase(
        string id,
        string packageId,
        PackagePurchaseSnapshot snapshot,
        decimal purchasePrice,
        decimal amountDue,
        int total,
        int used,
        bool isActive,
        DateOnly expiresOn,
        int? releasedSessions = null) =>
        new()
        {
            Id = id,
            TenantId = TenantA,
            CustomerId = CustomerA,
            PackageId = packageId,
            PurchasePrice = purchasePrice,
            AmountDue = amountDue,
            ReleasedSessions = releasedSessions,
            PurchaseSnapshotJson = snapshot.ToJson(),
            Total = total,
            Used = used,
            ReservedSessions = 0,
            ExpiresOn = expiresOn,
            IsActive = isActive
        };

    private static CustomerPackageInstallment NewPurchaseInstallment(
        string id,
        int number,
        decimal amount,
        int cumulativeAvailableSessions,
        DateTime? collectedAtUtc = null) =>
        new()
        {
            Id = id,
            TenantId = TenantA,
            CustomerPackageId = DetailPurchaseA,
            Number = number,
            Amount = amount,
            CumulativeAvailableSessions = cumulativeAvailableSessions,
            CollectedAtUtc = collectedAtUtc,
            PaymentMethod = collectedAtUtc is null ? null : "cash"
        };

    private static InstallmentCollection NewCollection(
        string id,
        string purchaseId,
        int installmentNumber,
        string idempotencyKey) =>
        new()
        {
            Id = id,
            TenantId = TenantA,
            CustomerPackageId = purchaseId,
            InstallmentNumber = installmentNumber,
            Amount = 500,
            PaymentMethod = "cash",
            IdempotencyKey = idempotencyKey,
            CollectedByUserId = UserA,
            ConfirmedAtUtc = DateTime.UtcNow.AddDays(-installmentNumber)
        };

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
