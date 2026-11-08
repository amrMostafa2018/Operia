using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Customers.Queries.GetCustomerPurchaseDetails;
using Operia.Application.Packages;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Customers;

public sealed class GetCustomerPurchaseDetailsHandlerTests
{
    [Fact]
    public async Task ShowsSavedPriceBalancesAndOnlyCompletedExecution()
    {
        var user = User("tenant-1");
        var clock = Clock();
        await using var db = Context($"PurchaseInquiry_{Guid.NewGuid()}", clock.Object, user.Object);
        var offer = new Package
        {
            Id = "offer-1",
            TenantId = "tenant-1",
            Name = "Changed offer",
            OfferType = OfferType.Package,
            PulseCount = 8000,
            Price = 30000m
        };
        db.Packages.Add(offer);
        db.Customers.Add(new Customer
        {
            Id = "customer-1",
            TenantId = "tenant-1",
            FullName = "Customer",
            MobileNumber = "01012345678",
            NormalizedMobileNumber = "01012345678"
        });
        var snapshot = new PackagePurchaseSnapshot(
            "Original offer", OfferType.Package, 60, null, 8000, 12, "category-1", null,
            "installment", 20000m, 20000m, null, 5000m,
            [new PurchaseInstallmentSnapshot(1, 5000m, 0, 3000),
             new PurchaseInstallmentSnapshot(2, 5000m, 0, 5000),
             new PurchaseInstallmentSnapshot(3, 5000m, 0, 7000)]);
        db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "purchase-1",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "offer-1",
            Total = 8000,
            Used = 2500,
            ReleasedSessions = 3000,
            PurchasePrice = 20000m,
            AmountDue = 15000m,
            StartedOn = new DateOnly(2026, 10, 12),
            PurchaseSnapshotJson = snapshot.ToJson(),
            ExpiresOn = new DateOnly(2027, 1, 1)
        });
        db.Bookings.AddRange(
            new Booking
            {
                Id = "completed-booking",
                TenantId = "tenant-1",
                CustomerId = "customer-1",
                EmployeeId = "employee-1",
                BranchId = "branch-1",
                ScheduledDate = new DateOnly(2026, 10, 20),
                StartMinutes = 600,
                EndMinutes = 660,
                Status = BookingStatus.Completed
            },
            new Booking
            {
                Id = "future-booking",
                TenantId = "tenant-1",
                CustomerId = "customer-1",
                EmployeeId = "employee-1",
                BranchId = "branch-1",
                ScheduledDate = new DateOnly(2026, 11, 20),
                StartMinutes = 600,
                EndMinutes = 660,
                Status = BookingStatus.Booked
            });
        db.BookingItems.Add(new BookingItem
        {
            Id = "item-1",
            TenantId = "tenant-1",
            BookingId = "completed-booking",
            PackageId = "offer-1",
            Name = "Original offer",
            Quantity = 1
        });
        db.BookingPackageReservations.AddRange(
            new BookingPackageReservation
            {
                TenantId = "tenant-1",
                BookingId = "completed-booking",
                CustomerPackageId = "purchase-1",
                SessionNumber = 1
            },
            new BookingPackageReservation
            {
                TenantId = "tenant-1",
                BookingId = "future-booking",
                CustomerPackageId = "purchase-1",
                SessionNumber = 2
            });
        db.BookingHistory.Add(new BookingHistory
        {
            TenantId = "tenant-1",
            BookingId = "completed-booking",
            Action = "Closed",
            ChangesJson = JsonSerializer.Serialize(new
            {
                Items = new[]
                {
                    new
                    {
                        BookingItemId = "item-1",
                        Status = "complete",
                        PulsesUsed = 2500,
                        Notes = "Performed",
                        CustomerPackageIds = new[] { "purchase-1" }
                    }
                }
            })
        });
        await db.SaveChangesAsync();

        var result = await new GetCustomerPurchaseDetailsHandler(db, user.Object, clock.Object)
            .Handle(new GetCustomerPurchaseDetailsQuery("purchase-1"), CancellationToken.None);

        result.PurchasePrice.Should().Be(20000m);
        result.AmountPaid.Should().Be(5000m);
        result.AmountRemaining.Should().Be(15000m);
        result.Pulses.Should().Be(new PurchaseBalanceDto(8000, 2500, 5500, 500, 3000));
        result.Sessions.Should().BeNull();
        result.StartsOn.Should().Be(new DateOnly(2026, 10, 12));
        result.NextInstallmentNumber.Should().Be(1);
        result.Installments.Should().OnlyContain(x => x.Status == "due");
        result.CompletedSessions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new CompletedPurchaseSessionDto(
                1, new DateOnly(2026, 10, 20), 600, null, 2500, "Performed", "Original offer"));
        var outsideBranch = await new GetCustomerPurchaseDetailsHandler(db, user.Object, clock.Object)
            .Handle(new GetCustomerPurchaseDetailsQuery("purchase-1", ["branch-2"]), CancellationToken.None);
        outsideBranch.CompletedSessions.Should().BeEmpty();

        var storedPurchase = await db.CustomerPackages.SingleAsync(x => x.Id == "purchase-1");
        storedPurchase.AmountDue = 10000m;
        storedPurchase.ReleasedSessions = 5000;
        await db.SaveChangesAsync();
        var afterCollection = await new GetCustomerPurchaseDetailsHandler(db, user.Object, clock.Object)
            .Handle(new GetCustomerPurchaseDetailsQuery("purchase-1"), CancellationToken.None);
        afterCollection.Pulses!.Released.Should().Be(5000);
        afterCollection.Installments.Select(x => x.Status).Should().Equal("paid", "due", "due");
        afterCollection.NextInstallmentNumber.Should().Be(2);

        storedPurchase.IsActive = false;
        storedPurchase.ReleasedSessions = storedPurchase.Used;
        db.InstallmentCollections.Add(new InstallmentCollection
        {
            TenantId = "tenant-1",
            CustomerPackageId = storedPurchase.Id,
            InstallmentNumber = 2,
            Amount = 5000m,
            IdempotencyKey = "confirmed-installment"
        });
        await db.SaveChangesAsync();
        var cancelled = await new GetCustomerPurchaseDetailsHandler(db, user.Object, clock.Object)
            .Handle(new GetCustomerPurchaseDetailsQuery("purchase-1"), CancellationToken.None);
        cancelled.Pulses!.Released.Should().Be(5000);
        cancelled.Pulses.Bookable.Should().Be(0);
    }

    [Fact]
    public async Task UsesPersistedCollectionsAndKeepsCancelledPurchaseFinancialHistory()
    {
        var user = User("tenant-1");
        var clock = Clock();
        await using var db = Context($"PurchaseInquiry_{Guid.NewGuid()}", clock.Object, user.Object);
        db.Packages.Add(new Package
        {
            Id = "offer-1",
            TenantId = "tenant-1",
            Name = "Current offer",
            OfferType = OfferType.Package,
            SessionCount = 10
        });
        db.Customers.Add(new Customer
        {
            Id = "customer-1",
            TenantId = "tenant-1",
            FullName = "Customer",
            MobileNumber = "01012345678",
            NormalizedMobileNumber = "01012345678"
        });
        db.Employees.Add(new Employee
        {
            Id = "employee-1",
            TenantId = "tenant-1",
            IdentityUserId = "cashier-1",
            FullName = "Reception User"
        });
        db.Sales.Add(new Sale
        {
            Id = "sale-1",
            TenantId = "tenant-1",
            BranchId = "branch-1",
            CustomerId = "customer-1"
        });
        var snapshot = new PackagePurchaseSnapshot(
            "Original offer", OfferType.Package, 60, 10, null, 12, "category-1", null,
            "installment", 20000m, 20000m, null, 5000m,
            [new PurchaseInstallmentSnapshot(1, 5000m, 2, 2),
             new PurchaseInstallmentSnapshot(2, 5000m, 2, 4),
             new PurchaseInstallmentSnapshot(3, 5000m, 3, 7)]);
        var purchase = new CustomerPackage
        {
            Id = "purchase-1",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "offer-1",
            SaleId = "sale-1",
            Total = 10,
            Used = 2,
            PurchasePrice = 20000m,
            AmountDue = 10000m,
            PurchaseSnapshotJson = snapshot.ToJson(),
            ExpiresOn = new DateOnly(2027, 1, 1),
            IsActive = false
        };
        purchase.Installments.Add(new CustomerPackageInstallment
        {
            Id = "first-payment",
            TenantId = "tenant-1",
            CustomerPackageId = purchase.Id,
            Number = 0,
            Amount = 5000m,
            CollectedAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
            PaymentMethod = "cash"
        });
        purchase.Installments.Add(new CustomerPackageInstallment
        {
            TenantId = "tenant-1",
            CustomerPackageId = purchase.Id,
            Number = 1,
            Amount = 5000m,
            CumulativeAvailableSessions = 2,
            CollectedAtUtc = new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc),
            PaymentMethod = "cash"
        });
        purchase.Installments.Add(new CustomerPackageInstallment
        {
            TenantId = "tenant-1",
            CustomerPackageId = purchase.Id,
            Number = 2,
            Amount = 5000m,
            CumulativeAvailableSessions = 4
        });
        purchase.Installments.Add(new CustomerPackageInstallment
        {
            TenantId = "tenant-1",
            CustomerPackageId = purchase.Id,
            Number = 3,
            Amount = 5000m,
            CumulativeAvailableSessions = 7
        });
        db.CustomerPackages.Add(purchase);
        db.SaleCollections.Add(new SaleCollection
        {
            Id = "sale-collection-1",
            TenantId = "tenant-1",
            SaleId = "sale-1",
            Amount = 5000m,
            PaymentMethod = "cash",
            Confirmed = true,
            IdempotencyKey = "sale-key",
            CollectedByUserId = "cashier-1",
            AttemptedAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
            ConfirmedAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)
        });
        db.InstallmentCollections.Add(new InstallmentCollection
        {
            Id = "installment-collection-1",
            TenantId = "tenant-1",
            CustomerPackageId = purchase.Id,
            InstallmentNumber = 1,
            Amount = 5000m,
            PaymentMethod = "cash",
            IdempotencyKey = "installment-key",
            CollectedByUserId = "cashier-1",
            ConfirmedAtUtc = new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();

        var result = await new GetCustomerPurchaseDetailsHandler(db, user.Object, clock.Object)
            .Handle(new GetCustomerPurchaseDetailsQuery(purchase.Id), CancellationToken.None);

        result.AmountPaid.Should().Be(10000m);
        result.AmountRemaining.Should().Be(10000m);
        result.PaymentStatus.Should().Be("partial");
        result.PackageStatus.Should().Be("cancelled");
        result.Installments.Select(x => x.Status).Should().Equal("paid", "cancelled", "cancelled");
        result.NextInstallmentNumber.Should().BeNull();
        result.Collections.Should().HaveCount(2);
        result.Collections[0].Should().BeEquivalentTo(new PurchaseCollectionDto(
            "sale-collection-1", 0, 5000m, "cash",
            new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
            "sale-key", "Reception User"));
        result.Collections[1].RecordedBy.Should().Be("Reception User");
    }

    [Fact]
    public async Task CannotReadAnotherTenantsPurchase()
    {
        var database = $"PurchaseInquiry_{Guid.NewGuid()}";
        var clock = Clock();
        var other = User("tenant-2");
        await using (var db = Context(database, clock.Object, other.Object))
        {
            db.Packages.Add(new Package { Id = "offer-2", TenantId = "tenant-2", Name = "Other" });
            db.Customers.Add(new Customer
            {
                Id = "customer-2",
                TenantId = "tenant-2",
                FullName = "Other",
                MobileNumber = "01012345678",
                NormalizedMobileNumber = "01012345678"
            });
            db.CustomerPackages.Add(new CustomerPackage
            {
                Id = "purchase-2",
                TenantId = "tenant-2",
                CustomerId = "customer-2",
                PackageId = "offer-2",
                Total = 5
            });
            await db.SaveChangesAsync();
        }

        var user = User("tenant-1");
        await using var currentDb = Context(database, clock.Object, user.Object);
        var act = () => new GetCustomerPurchaseDetailsHandler(currentDb, user.Object, clock.Object)
            .Handle(new GetCustomerPurchaseDetailsQuery("purchase-2"), CancellationToken.None);

        await act.Should().ThrowAsync<ApiNotFoundException>();
    }

    private static Mock<ICurrentUserService> User(string tenantId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns(tenantId);
        user.SetupGet(x => x.UserId).Returns("operator-1");
        return user;
    }

    private static Mock<IDateTimeProvider> Clock()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 10, 27, 8, 0, 0, DateTimeKind.Utc));
        return clock;
    }

    private static TestApplicationDbContext Context(
        string database,
        IDateTimeProvider clock,
        ICurrentUserService user)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(database)
            .Options;
        return new TestApplicationDbContext(options, clock, user);
    }
}
