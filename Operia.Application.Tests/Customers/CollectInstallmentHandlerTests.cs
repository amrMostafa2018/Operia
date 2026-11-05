using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Customers.Commands.CollectInstallment;
using Operia.Application.Packages;
using Operia.Application.Revenue.Queries.ListInstallmentRevenue;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Customers;

public sealed class CollectInstallmentHandlerTests
{
    [Fact]
    public async Task RejectsLaterInstallmentUntilFirstPaymentIsCollected()
    {
        await using var fixture = await Fixture.CreateAsync("tenant-1");
        var purchase = await fixture.Db.CustomerPackages.Include(x => x.Installments).SingleAsync();
        purchase.AmountDue = 15m;
        purchase.Installments.Single(x => x.Number == 0).CollectedAtUtc = null;
        await fixture.Db.SaveChangesAsync();

        var collect = () => fixture.Collect("before-first-payment");
        (await collect.Should().ThrowAsync<ConflictException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Customers.InstallmentNotEligible);
        (await fixture.Db.InstallmentCollections.CountAsync()).Should().Be(0);
        purchase.AmountDue.Should().Be(15m);
    }

    [Fact]
    public async Task BothEndpointsShareCollectionState()
    {
        await using var fixture = await Fixture.CreateAsync("tenant-1");
        await fixture.Collect("new-first");
        var oldHandler = fixture.LegacyCollector();

        var retry = await oldHandler.Handle(
            new Operia.Application.Packages.InstallmentPurchases.CollectInstallmentCommand(
                "purchase-1", 1, 5m, "cash"), CancellationToken.None);
        retry.AmountDue.Should().Be(5m);
        (await fixture.Db.InstallmentCollections.CountAsync()).Should().Be(1);

        var purchase = await fixture.Db.CustomerPackages.SingleAsync();
        purchase.Used = 2;
        await fixture.Db.SaveChangesAsync();
        await oldHandler.Handle(
            new Operia.Application.Packages.InstallmentPurchases.CollectInstallmentCommand(
                "purchase-1", 2, 5m, "cash"), CancellationToken.None);
        purchase.AmountDue.Should().Be(0m);
        (await fixture.Revenue()).Total.Should().Be(2);
    }

    [Fact]
    public async Task CollectsNextRowOnce_WaitsForProgress_AndReleasesRemainderOnFullPayment()
    {
        await using var fixture = await Fixture.CreateAsync("tenant-1");
        var first = await fixture.Collect("first");
        first.InstallmentNumber.Should().Be(1);
        first.AmountRemaining.Should().Be(5m);
        first.ReleasedSessions.Should().Be(2);
        (await fixture.Collect("first")).CollectionId.Should().Be(first.CollectionId);
        (await fixture.Db.InstallmentCollections.CountAsync()).Should().Be(1);

        var tooEarly = () => fixture.Collect("second");
        (await tooEarly.Should().ThrowAsync<ConflictException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Customers.InstallmentNotEligible);
        (await fixture.Db.CustomerPackages.SingleAsync()).ReleasedSessions.Should().Be(2);

        var purchase = await fixture.Db.CustomerPackages.SingleAsync();
        purchase.Used = 2;
        await fixture.Db.SaveChangesAsync();
        var final = await fixture.Collect("second");
        final.InstallmentNumber.Should().Be(2);
        final.FullyPaid.Should().BeTrue();
        final.AmountRemaining.Should().Be(0m);
        final.ReleasedSessions.Should().Be(5);
        (await fixture.Collect("second")).CollectionId.Should().Be(final.CollectionId);
        (await fixture.Db.InstallmentCollections.CountAsync()).Should().Be(2);
        (await fixture.Db.AuditLogs.CountAsync(x => x.Action == AuditActions.InstallmentCollected))
            .Should().Be(2);
        purchase.ExpiresOn.Should().Be(new DateOnly(2027, 1, 1));
    }

    [Fact]
    public async Task RejectsAnotherTenantAndDisabledPaymentMethodWithoutChangingBalance()
    {
        await using var fixture = await Fixture.CreateAsync("tenant-1");
        var disabled = () => fixture.Collect("disabled", "bank_transfer");
        (await disabled.Should().ThrowAsync<ConflictException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Customers.InstallmentPaymentMethodUnavailable);
        (await fixture.Db.CustomerPackages.SingleAsync()).AmountDue.Should().Be(10m);

        await using var otherTenant = await Fixture.OpenAsync(fixture.Database, "tenant-2");
        var crossTenant = () => otherTenant.Collect("cross-tenant");
        await crossTenant.Should().ThrowAsync<ApiNotFoundException>();
        (await fixture.Db.InstallmentCollections.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UnconfirmedEntitlementIsNeverBookable()
    {
        await using var fixture = await Fixture.CreateAsync("tenant-1");
        var purchase = await fixture.Db.CustomerPackages.SingleAsync();
        PurchaseSessionEntitlement.Released(purchase).Should().Be(0);
        purchase.ReleasedSessions = 2;
        PurchaseSessionEntitlement.Released(purchase).Should().Be(2);
        purchase.AmountDue = 0m;
        PurchaseSessionEntitlement.Released(purchase).Should().Be(5);
    }

    [Fact]
    public async Task ConfirmedCollectionAppearsInRevenueForItsTenantOnly()
    {
        await using var fixture = await Fixture.CreateAsync("tenant-1");
        await fixture.Collect("revenue-first");
        var ownRevenue = await fixture.Revenue();
        ownRevenue.Total.Should().Be(1);
        ownRevenue.Items.Single().Amount.Should().Be(5m);

        await using var otherTenant = await Fixture.OpenAsync(fixture.Database, "tenant-2");
        (await otherTenant.Revenue()).Total.Should().Be(0);
    }

    [Fact]
    public async Task LegacyCollectionCannotBeCollectedAgainThroughCustomerRoute()
    {
        await using var fixture = await Fixture.CreateAsync("tenant-1");
        var row = await fixture.Db.CustomerPackageInstallments.SingleAsync(x => x.Number == 1);
        row.CollectedAtUtc = new DateTime(2026, 10, 27, 7, 0, 0, DateTimeKind.Utc);
        row.PaymentMethod = "cash";
        await fixture.Db.SaveChangesAsync();

        var repeated = () => fixture.Collect("customer-route");
        (await repeated.Should().ThrowAsync<ConflictException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Customers.InstallmentAlreadyCollected);
        (await fixture.Db.CustomerPackages.SingleAsync()).AmountDue.Should().Be(10m);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly Mock<ICurrentUserService> _user;
        private readonly Mock<IDateTimeProvider> _clock;
        public string Database { get; }
        public TestApplicationDbContext Db { get; }

        private Fixture(string database, TestApplicationDbContext db,
            Mock<ICurrentUserService> user, Mock<IDateTimeProvider> clock)
        {
            Database = database;
            Db = db;
            _user = user;
            _clock = clock;
        }

        public static async Task<Fixture> CreateAsync(string tenantId)
        {
            var fixture = await OpenAsync($"InstallmentCollection_{Guid.NewGuid()}", tenantId);
            fixture.Db.Customers.Add(new Customer
            {
                Id = "customer-1",
                TenantId = tenantId,
                FullName = "Customer",
                MobileNumber = "01012345678",
                NormalizedMobileNumber = "01012345678"
            });
            fixture.Db.Packages.Add(new Package
            {
                Id = "offer-1",
                TenantId = tenantId,
                Name = "Sessions",
                OfferType = OfferType.Package,
                SessionCount = 5,
                Status = PackageStatus.Active
            });
            var snapshot = new PackagePurchaseSnapshot(
                "Sessions", OfferType.Package, 60, 5, null, 12, "category-1", null,
                "installment", 15m, 15m, null, 5m,
                [new PurchaseInstallmentSnapshot(1, 5m, 2, 2),
                 new PurchaseInstallmentSnapshot(2, 5m, 0, 2)]);
            fixture.Db.CustomerPackages.Add(new CustomerPackage
            {
                Id = "purchase-1",
                TenantId = tenantId,
                CustomerId = "customer-1",
                PackageId = "offer-1",
                PurchasePrice = 15m,
                AmountDue = 10m,
                PurchaseSnapshotJson = snapshot.ToJson(),
                Total = 5,
                Used = 0,
                ExpiresOn = new DateOnly(2027, 1, 1),
                IsActive = true
            });
            fixture.Db.CustomerPackageInstallments.AddRange(
                new CustomerPackageInstallment
                {
                    TenantId = tenantId,
                    CustomerPackageId = "purchase-1",
                    Number = 0,
                    Amount = 5m,
                    CollectedAtUtc = new DateTime(2026, 10, 27, 7, 0, 0, DateTimeKind.Utc),
                    PaymentMethod = "cash"
                },
                new CustomerPackageInstallment
                {
                    TenantId = tenantId,
                    CustomerPackageId = "purchase-1",
                    Number = 1,
                    Amount = 5m,
                    CumulativeAvailableSessions = 2
                },
                new CustomerPackageInstallment
                {
                    TenantId = tenantId,
                    CustomerPackageId = "purchase-1",
                    Number = 2,
                    Amount = 5m,
                    CumulativeAvailableSessions = 2
                });
            await fixture.Db.SaveChangesAsync();
            return fixture;
        }

        public static Task<Fixture> OpenAsync(string database, string tenantId)
        {
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.TenantId).Returns(tenantId);
            user.SetupGet(x => x.UserId).Returns("operator-1");
            var clock = new Mock<IDateTimeProvider>();
            clock.SetupGet(x => x.UtcNow)
                .Returns(new DateTime(2026, 10, 27, 8, 0, 0, DateTimeKind.Utc));
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(database).Options;
            return Task.FromResult(new Fixture(database,
                new TestApplicationDbContext(options, clock.Object, user.Object), user, clock));
        }

        public Task<CollectInstallmentResult> Collect(string key, string method = "cash")
        {
            var handler = new CollectInstallmentHandler(Db, new InstallmentCollectionStore(Db), _user.Object,
                new AuditWriter(Db, _user.Object), _clock.Object);
            return handler.Handle(new CollectInstallmentCommand("purchase-1", method, key),
                CancellationToken.None);
        }

        public Operia.Application.Packages.InstallmentPurchases.CollectInstallmentHandler LegacyCollector() =>
            new(Db, new InstallmentCollectionStore(Db), _user.Object,
                new AuditWriter(Db, _user.Object), _clock.Object);

        public Task<InstallmentRevenuePage> Revenue() =>
            new ListInstallmentRevenueHandler(Db, _user.Object)
                .Handle(new ListInstallmentRevenueQuery(), CancellationToken.None);

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
