using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Customers.Queries.FindCustomerPurchases;
using Operia.Application.Packages;
using Operia.Application.Tests.Helpers;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Customers;

public sealed class FindCustomerPurchasesHandlerTests
{
    [Fact]
    public async Task ReturnsEachPurchaseIncludingCancelledAndKeepsCustomerRegistrationDate()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 10, 27, 8, 0, 0, DateTimeKind.Utc));
        var user = User("tenant-1");
        await using var db = Context($"Inquiry_{Guid.NewGuid()}", clock.Object, user.Object);
        db.Customers.Add(new Customer
        {
            Id = "customer-1",
            TenantId = "tenant-1",
            FullName = "Before",
            MobileNumber = "01012345678",
            NormalizedMobileNumber = "01012345678",
            IsActive = true
        });
        var offer = new Package
        {
            Id = "offer-1",
            TenantId = "tenant-1",
            Name = "Current name",
            OfferType = OfferType.Package
        };
        db.Packages.Add(offer);
        await db.SaveChangesAsync();
        var registeredAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var customer = await db.Customers.SingleAsync();
        customer.CreatedAt = registeredAt;
        customer.FullName = "After rename";
        var snapshot = PackagePurchaseSnapshot.Capture(offer, "cash", 100m).ToJson();
        db.CustomerPackages.AddRange(
            new CustomerPackage
            {
                Id = "purchase-1",
                TenantId = "tenant-1",
                CustomerId = customer.Id,
                PackageId = offer.Id,
                Total = 5,
                PurchaseSnapshotJson = snapshot
            },
            new CustomerPackage
            {
                Id = "purchase-2",
                TenantId = "tenant-1",
                CustomerId = customer.Id,
                PackageId = offer.Id,
                Total = 5,
                IsActive = false,
                PurchaseSnapshotJson = snapshot
            });
        await db.SaveChangesAsync();

        var result = await new FindCustomerPurchasesHandler(db, user.Object, clock.Object)
            .Handle(new FindCustomerPurchasesQuery("010 1234 5678"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.FullName.Should().Be("After rename");
        result.RegisteredAt.Should().Be(registeredAt);
        result.OperationCount.Should().Be(2);
        result.Purchases.Select(x => x.Id).Should().BeEquivalentTo(["purchase-1", "purchase-2"]);
        result.Purchases.Select(x => x.OfferName).Should().OnlyContain(x => x == "Current name");
        result.Purchases.Single(x => x.Id == "purchase-2").Status.Should().Be("cancelled");
    }

    [Fact]
    public async Task DoesNotFindAnotherTenantsCustomerWithTheSameMobile()
    {
        var database = $"Inquiry_{Guid.NewGuid()}";
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 10, 27, 8, 0, 0, DateTimeKind.Utc));
        var otherUser = User("tenant-2");
        await using (var otherDb = Context(database, clock.Object, otherUser.Object))
        {
            otherDb.Customers.Add(new Customer
            {
                TenantId = "tenant-2",
                FullName = "Other tenant",
                MobileNumber = "01012345678",
                NormalizedMobileNumber = "01012345678"
            });
            await otherDb.SaveChangesAsync();
        }

        var user = User("tenant-1");
        await using var db = Context(database, clock.Object, user.Object);
        var result = await new FindCustomerPurchasesHandler(db, user.Object, clock.Object)
            .Handle(new FindCustomerPurchasesQuery("01012345678"), CancellationToken.None);

        result.Should().BeNull();
    }

    private static Mock<ICurrentUserService> User(string tenantId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns(tenantId);
        user.SetupGet(x => x.UserId).Returns("operator-1");
        return user;
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
