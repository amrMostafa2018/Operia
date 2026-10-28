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
            [new PurchaseInstallmentSnapshot(1, 5000m, 0, 0),
             new PurchaseInstallmentSnapshot(2, 5000m, 0, 0),
             new PurchaseInstallmentSnapshot(3, 5000m, 0, 0)]);
        db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "purchase-1",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "offer-1",
            Total = 8000,
            Used = 2500,
            PurchasePrice = 20000m,
            AmountDue = 15000m,
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
        result.Pulses.Should().Be(new PurchaseBalanceDto(8000, 2500, 5500));
        result.Sessions.Should().BeNull();
        result.StartsOn.Should().BeNull();
        result.NextInstallmentNumber.Should().Be(1);
        result.Installments.Should().OnlyContain(x => x.Status == "due");
        result.CompletedSessions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new CompletedPurchaseSessionDto(
                1, new DateOnly(2026, 10, 20), 600, null, 2500, "Performed"));
        var outsideBranch = await new GetCustomerPurchaseDetailsHandler(db, user.Object, clock.Object)
            .Handle(new GetCustomerPurchaseDetailsQuery("purchase-1", ["branch-2"]), CancellationToken.None);
        outsideBranch.CompletedSessions.Should().BeEmpty();

        var storedPurchase = await db.CustomerPackages.SingleAsync(x => x.Id == "purchase-1");
        storedPurchase.AmountDue = 10000m;
        await db.SaveChangesAsync();
        var afterCollection = await new GetCustomerPurchaseDetailsHandler(db, user.Object, clock.Object)
            .Handle(new GetCustomerPurchaseDetailsQuery("purchase-1"), CancellationToken.None);
        afterCollection.Installments.Select(x => x.Status).Should().Equal("paid", "due", "due");
        afterCollection.NextInstallmentNumber.Should().Be(2);
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
