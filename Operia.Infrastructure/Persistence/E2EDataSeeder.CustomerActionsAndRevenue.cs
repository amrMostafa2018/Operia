using System.Text.Json;
using Operia.Application.Common.Auditing;
using Operia.Application.Packages;
using Operia.Application.Sales.Commands.ConfirmSale;
using Operia.Domain.Entities;
using Operia.Domain.Enums;

namespace Operia.Infrastructure.Persistence;

public static partial class E2EDataSeeder
{
    private static readonly JsonSerializerOptions CustomerActionAuditJsonOptions =
        new(JsonSerializerDefaults.Web);

    private const string ActionOffer = "e2e70000-0000-4000-8000-000000000200";
    private const string RefundCustomer = "e2e70000-0000-4000-8000-000000000201";
    private const string RefundSale = "e2e70000-0000-4000-8000-000000000202";
    private const string RefundPurchase = "e2e70000-0000-4000-8000-000000000203";
    private const string RefundCollection = "e2e70000-0000-4000-8000-000000000204";
    private const string RefundBooking = "e2e70000-0000-4000-8000-000000000205";
    private const string BlockCustomer = "e2e70000-0000-4000-8000-000000000210";
    private const string UnblockCustomer = "e2e70000-0000-4000-8000-000000000211";
    private const string HistoryCustomer = "e2e70000-0000-4000-8000-000000000212";
    private const string HistoryOtherCustomer = "e2e70000-0000-4000-8000-000000000213";
    private const string HistorySale = "e2e70000-0000-4000-8000-000000000214";
    private const string HistoryOtherSale = "e2e70000-0000-4000-8000-000000000215";
    private const string BlockSale = "e2e70000-0000-4000-8000-000000000216";
    private const string UnblockSale = "e2e70000-0000-4000-8000-000000000217";
    private const string RevenueCustomer = "e2e70000-0000-4000-8000-000000000220";
    private const string RevenueTodaySale = "e2e70000-0000-4000-8000-000000000221";
    private const string RevenueTodayPurchase = "e2e70000-0000-4000-8000-000000000222";
    private const string RevenueMonthSale = "e2e70000-0000-4000-8000-000000000223";
    private const string RevenuePreviousSale = "e2e70000-0000-4000-8000-000000000224";
    private const string RevenuePendingSale = "e2e70000-0000-4000-8000-000000000225";
    private const string RevenueFailedSale = "e2e70000-0000-4000-8000-000000000226";
    private const string RevenuePaymentSale = "e2e70000-0000-4000-8000-000000000227";
    private const string RevenueInstallmentSale = "e2e70000-0000-4000-8000-000000000228";
    private const string RevenueInstallmentPurchase = "e2e70000-0000-4000-8000-000000000229";

    private static void SeedCustomerActionsAndRevenue(
        ApplicationDbContext db, DateOnly utcToday, Package installmentOffer,
        string installmentItems)
    {
        var timezone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var nowUtc = DateTime.UtcNow;
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timezone));
        var monthStart = new DateOnly(localToday.Year, localToday.Month, 1);
        var monthUtc = localToday.Day == 1 ? nowUtc :
            TimeZoneInfo.ConvertTimeToUtc(monthStart.ToDateTime(new TimeOnly(12, 0)), timezone);
        var previousUtc = TimeZoneInfo.ConvertTimeToUtc(
            monthStart.AddDays(-1).ToDateTime(new TimeOnly(12, 0)), timezone);

        var offer = new Package
        {
            Id = ActionOffer,
            TenantId = TenantA,
            BusinessId = BusinessA,
            ServiceCategoryId = CategoryA,
            Name = "E2E US-21 Refund Package",
            Description = "Reserved E2E action prerequisite",
            OfferType = OfferType.Package,
            SessionDurationMinutes = 30,
            SessionCount = 10,
            PackageExpiryMonths = 12,
            Price = 5000m,
            CashEnabled = true,
            AllStaff = true,
            Status = PackageStatus.Active
        };
        db.Packages.Add(offer);
        db.Customers.AddRange(
            NewCustomer(RefundCustomer, "US-21 Refund Customer", "01000000201"),
            NewCustomer(BlockCustomer, "US-21 Block Customer", "01000000210"),
            NewCustomer(UnblockCustomer, "US-21 Unblock Customer", "01000000211", true,
                "Existing center restriction"),
            NewCustomer(HistoryCustomer, "US-22 History Customer", "01000000212"),
            NewCustomer(HistoryOtherCustomer, "US-22 Other Customer", "01000000213"),
            NewCustomer(RevenueCustomer, "US-23 Revenue Customer", "01000000220"));

        var refundItems = SaleItems(offer, 5000m);
        db.Sales.AddRange(
            NewSale(RefundSale, RefundCustomer, BranchA, 5000m, refundItems, "Closed"),
            NewSale(HistorySale, HistoryCustomer, BranchA, 2000m,
                installmentItems, "PendingCollection"),
            NewSale(HistoryOtherSale, HistoryOtherCustomer, BranchA, 2000m,
                installmentItems, "PendingCollection"),
            NewSale(BlockSale, BlockCustomer, BranchA, 2000m,
                installmentItems, "PendingCollection"),
            NewSale(UnblockSale, UnblockCustomer, BranchA, 2000m,
                installmentItems, "PendingCollection"),
            NewSale(RevenueTodaySale, RevenueCustomer, SecondaryBranchA, 5000m,
                refundItems, "Closed"),
            NewSale(RevenueMonthSale, RevenueCustomer, SecondaryBranchA, 1200m,
                SaleItems(offer, 1200m), "Closed"),
            NewSale(RevenuePreviousSale, RevenueCustomer, SecondaryBranchA, 700m,
                SaleItems(offer, 700m), "Closed"),
            NewSale(RevenuePendingSale, RevenueCustomer, SecondaryBranchA, 300m,
                SaleItems(offer, 300m), "PendingCollection"),
            NewSale(RevenueFailedSale, RevenueCustomer, SecondaryBranchA, 300m,
                SaleItems(offer, 300m), "PendingCollection"),
            NewSale(RevenuePaymentSale, RevenueCustomer, SecondaryBranchA, 300m,
                SaleItems(offer, 300m), "PendingCollection"),
            new Sale
            {
                Id = RevenueInstallmentSale,
                TenantId = TenantA,
                BranchId = SecondaryBranchA,
                CustomerId = RevenueCustomer,
                IdempotencyKey = "e2e-us23-installment-prerequisite",
                RequestHash = "seeded-us23-installment",
                PaymentMethod = "cash",
                GrandTotal = 2000m,
                DueNow = 500m,
                ItemsSnapshotJson = installmentItems,
                Disposition = "Later",
                Status = "Closed"
            });

        db.SaleCollections.AddRange(
            NewCollection(RefundCollection, RefundSale, 5000m, nowUtc.AddDays(-7)),
            NewCollection("e2e70000-0000-4000-8000-000000000230",
                RevenueTodaySale, 5000m, nowUtc),
            NewCollection("e2e70000-0000-4000-8000-000000000231",
                RevenueMonthSale, 1200m, monthUtc),
            NewCollection("e2e70000-0000-4000-8000-000000000232",
                RevenuePreviousSale, 700m, previousUtc),
            NewCollection("e2e70000-0000-4000-8000-000000000234",
                RevenueInstallmentSale, 500m, monthUtc),
            new SaleCollection
            {
                Id = "e2e70000-0000-4000-8000-000000000233",
                TenantId = TenantA,
                SaleId = RevenueFailedSale,
                IdempotencyKey = "e2e-us23-failed-prerequisite",
                Amount = 300m,
                PaymentMethod = "cash",
                Confirmed = false,
                AttemptedAtUtc = nowUtc.AddMinutes(-4),
                CollectedByUserId = UserA
            });
        db.InstallmentCollections.Add(new InstallmentCollection
        {
            Id = "e2e70000-0000-4000-8000-000000000235",
            TenantId = TenantA,
            CustomerPackageId = RevenueInstallmentPurchase,
            InstallmentNumber = 1,
            Amount = 500m,
            PaymentMethod = "cash",
            IdempotencyKey = "e2e-us23-numbered-prerequisite",
            CollectedByUserId = UserA,
            ConfirmedAtUtc = nowUtc
        });

        db.CustomerPackages.AddRange(
            NewPurchase(RefundPurchase, RefundCustomer, RefundSale, offer,
                5000m, 3, 1, utcToday, BranchA),
            NewPurchase(RevenueTodayPurchase, RevenueCustomer, RevenueTodaySale, offer,
                5000m, 0, 0, utcToday, SecondaryBranchA),
            new CustomerPackage
            {
                Id = RevenueInstallmentPurchase,
                TenantId = TenantA,
                CustomerId = RevenueCustomer,
                PackageId = installmentOffer.Id,
                SaleId = RevenueInstallmentSale,
                BranchId = SecondaryBranchA,
                BranchAttributionSource = "Sale",
                SaleItemIndex = 0,
                SaleUnitIndex = 0,
                PurchasePrice = 2000m,
                AmountDue = 1000m,
                ReleasedSessions = 3,
                PurchaseSnapshotJson = PackagePurchaseSnapshot
                    .Capture(installmentOffer, "installment", 2000m).ToJson(),
                Total = 10,
                ReservedSessions = 0,
                ExpiresOn = utcToday.AddMonths(12),
                IsActive = true
            });

        db.Bookings.Add(new Booking
        {
            Id = RefundBooking,
            TenantId = TenantA,
            BookingNumber = "E2E-US21-RESERVATION",
            IdempotencyKey = "e2e-us21-reservation-prerequisite",
            BranchId = BranchA,
            EmployeeId = StaffEmployeeA,
            CustomerId = RefundCustomer,
            CustomerName = "US-21 Refund Customer",
            CustomerMobile = "01000000201",
            ScheduledDate = utcToday.AddDays(10),
            StartMinutes = 720,
            EndMinutes = 750,
            Status = BookingStatus.Booked,
            PaymentMethod = "cash"
        });
        db.BookingItems.Add(new BookingItem
        {
            Id = "e2e70000-0000-4000-8000-000000000206",
            TenantId = TenantA,
            BookingId = RefundBooking,
            Type = BookingItemType.PackageSession,
            PackageId = ActionOffer,
            Name = offer.Name,
            Quantity = 1,
            DurationMinutes = 30,
            UnitPrice = 0,
            PaymentOption = "cash"
        });
        db.BookingPackageReservations.Add(new BookingPackageReservation
        {
            Id = "e2e70000-0000-4000-8000-000000000207",
            TenantId = TenantA,
            BookingId = RefundBooking,
            CustomerPackageId = RefundPurchase,
            SessionNumber = 4
        });

        // Historical prerequisites for a separate customer and sale, including a second page.
        for (var index = 0; index < 12; index++)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Id = $"e2e70000-0000-4000-8000-{260 + index:000000000000}",
                TenantId = TenantA,
                UserId = UserA,
                UserDisplayName = "US-07 E2E Owner",
                Action = AuditActions.CustomerBlockReasonChanged,
                EntityType = nameof(Customer),
                EntityId = HistoryCustomer,
                EntityName = "US-22 History Customer",
                DetailsJson = JsonSerializer.Serialize(new
                {
                    OldState = false,
                    NewState = false,
                    OldReason = $"review {index}",
                    NewReason = $"review {index + 1}"
                }, CustomerActionAuditJsonOptions)
            });
        }
        db.AuditLogs.Add(new AuditLog
        {
            Id = "e2e70000-0000-4000-8000-000000000272",
            TenantId = TenantA,
            UserId = UserA,
            Action = AuditActions.CustomerCreated,
            EntityType = nameof(Customer),
            EntityId = HistoryOtherCustomer,
            EntityName = "US-22 Other Customer"
        });
        db.AuditLogs.Add(new AuditLog
        {
            Id = "e2e70000-0000-4000-8000-000000000273",
            TenantId = TenantA,
            UserId = UserA,
            UserDisplayName = "US-07 E2E Owner",
            Action = AuditActions.CustomerBlocked,
            EntityType = nameof(Customer),
            EntityId = UnblockCustomer,
            EntityName = "US-21 Unblock Customer",
            DetailsJson = JsonSerializer.Serialize(new
            {
                OldState = false,
                NewState = true,
                OldReason = (string?)null,
                NewReason = "Existing center restriction"
            }, CustomerActionAuditJsonOptions)
        });
    }

    private static Customer NewCustomer(string id, string name, string mobile,
        bool blocked = false, string? reason = null) => new()
        {
            Id = id,
            TenantId = TenantA,
            FullName = name,
            MobileNumber = mobile,
            NormalizedMobileNumber = "2" + mobile,
            IsActive = true,
            IsBlocked = blocked,
            BlockReason = reason
        };

    private static string SaleItems(Package offer, decimal amount) =>
        JsonSerializer.Serialize(new[]
        {
            new ConfirmedSaleItemSnapshot(offer.Id, null, offer.Name, "package",
                offer.SessionDurationMinutes, offer.SessionCount, 1, "cash",
                amount, 0m, amount, amount,
                PackagePurchaseSnapshot.Capture(offer, "cash", amount))
        });

    private static Sale NewSale(string id, string customerId, string branchId,
        decimal amount, string items, string status) => new()
        {
            Id = id,
            TenantId = TenantA,
            BranchId = branchId,
            CustomerId = customerId,
            IdempotencyKey = $"e2e-{id}",
            RequestHash = $"seeded-{id}",
            PaymentMethod = "cash",
            GrandTotal = amount,
            DueNow = amount,
            ItemsSnapshotJson = items,
            Disposition = "Later",
            Status = status
        };

    private static SaleCollection NewCollection(string id, string saleId,
        decimal amount, DateTime atUtc) => new()
        {
            Id = id,
            TenantId = TenantA,
            SaleId = saleId,
            IdempotencyKey = $"e2e-{id}",
            Amount = amount,
            PaymentMethod = "cash",
            Confirmed = true,
            AttemptedAtUtc = atUtc,
            ConfirmedAtUtc = atUtc,
            CollectedByUserId = UserA
        };

    private static CustomerPackage NewPurchase(string id, string customerId,
        string saleId, Package offer, decimal amount, int used, int reserved,
        DateOnly today, string branchId) => new()
        {
            Id = id,
            TenantId = TenantA,
            CustomerId = customerId,
            PackageId = offer.Id,
            SaleId = saleId,
            BranchId = branchId,
            BranchAttributionSource = "Sale",
            SaleItemIndex = 0,
            SaleUnitIndex = 0,
            PurchasePrice = amount,
            AmountDue = 0m,
            ReleasedSessions = 10,
            PurchaseSnapshotJson = PackagePurchaseSnapshot.Capture(offer, "cash", amount).ToJson(),
            Total = 10,
            Used = used,
            ReservedSessions = reserved,
            ExpiresOn = today.AddMonths(12),
            IsActive = true
        };
}
