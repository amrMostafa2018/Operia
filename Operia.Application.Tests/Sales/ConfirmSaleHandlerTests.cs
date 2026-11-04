using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Sales.Commands.ConfirmSale;
using Operia.Application.Sales.Queries.QuoteSale;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Sales;

public sealed class ConfirmSaleHandlerTests
{
    [Fact]
    public async Task MixedOptions_SaveSnapshotAndAudit_WithoutCollectionOrBooking()
    {
        await using var fixture = await Fixture.CreateAsync();
        var items = new[]
        {
            Item("package", "installment"),
            Item("service", "cash")
        };
        var quote = await fixture.Quote(items);
        quote.GrandTotal.Should().Be(230m);
        quote.DueNow.Should().Be(80m);

        var first = await fixture.Confirm(items, quote, "mixed-1");
        var repeated = await fixture.Confirm(items, quote, "mixed-1");
        repeated.SaleId.Should().Be(first.SaleId);
        (await fixture.Db.Sales.CountAsync()).Should().Be(1);
        (await fixture.Db.AuditLogs.CountAsync(log => log.Action == AuditActions.SaleConfirmed))
            .Should().Be(1);
        (await fixture.Db.InstallmentCollections.CountAsync()).Should().Be(0);
        (await fixture.Db.Bookings.CountAsync()).Should().Be(0);

        fixture.Db.Packages.Single(offer => offer.Id == "package").Name = "Renamed";
        fixture.Db.Packages.Single(offer => offer.Id == "package").InstallmentPrice = 300m;
        await fixture.Db.SaveChangesAsync();
        var saved = await fixture.Db.Sales.SingleAsync();
        using var snapshot = JsonDocument.Parse(saved.ItemsSnapshotJson);
        snapshot.RootElement[0].GetProperty("Name").GetString().Should().Be("Package");
        snapshot.RootElement[0].GetProperty("FinalAmount").GetDecimal().Should().Be(200m);
        snapshot.RootElement[0].GetProperty("PurchaseTerms")
            .GetProperty("Installments").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task StoppedAfterQuote_RefusesConfirmationWithoutSaving()
    {
        await using var fixture = await Fixture.CreateAsync();
        var items = new[] { Item("service", "cash") };
        var quote = await fixture.Quote(items);
        fixture.Db.Packages.Single(offer => offer.Id == "service").Status = PackageStatus.Cancelled;
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Confirm(items, quote, "stopped-1");
        (await action.Should().ThrowAsync<ConflictException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Sales.OfferUnavailable);
        (await fixture.Db.Sales.CountAsync()).Should().Be(0);
        (await fixture.Db.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ChangedPriceAndUnlistedService_AreHandledWithoutCatalogMutation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var item = Item("service", "cash");
        var quote = await fixture.Quote([item]);
        fixture.Db.Packages.Single(offer => offer.Id == "service").Price = 40m;
        await fixture.Db.SaveChangesAsync();
        var changed = () => fixture.Confirm([item], quote, "old-price");
        (await changed.Should().ThrowAsync<ConflictException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Sales.QuoteChanged);

        var unlisted = new QuoteSaleItem(null, null, "Special service", 12m, 25, "cash");
        var unlistedQuote = await fixture.Quote([unlisted]);
        await fixture.Confirm([unlisted], unlistedQuote, "unlisted-1");
        (await fixture.Db.Packages.CountAsync()).Should().Be(2);
        (await fixture.Db.Sales.SingleAsync()).ItemsSnapshotJson.Should().Contain("Special service");
    }

    [Fact]
    public async Task DisabledPaymentAndForeignScope_AreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        var items = new[] { Item("service", "cash") };
        var quote = await fixture.Quote(items);
        var payment = () => fixture.Confirm(items, quote, "payment-1", "bank_transfer");
        (await payment.Should().ThrowAsync<ConflictException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Sales.PaymentMethodUnavailable);

        fixture.BranchScope.Setup(scope => scope.GetAllowedBranchIdsAsync("operator", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var branch = () => fixture.Confirm(items, quote, "branch-1");
        (await branch.Should().ThrowAsync<ForbiddenException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Sales.BranchUnavailable);
        (await fixture.Db.Sales.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AnotherTenantsOfferCannotBeQuoted()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddForeignOfferAsync();
        var action = () => fixture.Quote([Item("foreign-offer", "cash")]);
        (await action.Should().ThrowAsync<ConflictException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Sales.OfferUnavailable);
        (await fixture.Db.Sales.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OwnedSession_HasZeroDueAndDoesNotConsumeBalance()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "owned-1",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "service",
            PurchasePrice = 30m,
            AmountDue = 0m,
            Total = 1,
            Used = 0,
            ReleasedSessions = 1,
            IsActive = true
        });
        await fixture.Db.SaveChangesAsync();
        var items = new[] { new QuoteSaleItem(null, "owned-1", null, null, null, "owned") };
        var quote = await fixture.Quote(items);
        quote.DueNow.Should().Be(0m);
        var result = await fixture.Confirm(items, quote, "owned-sale", null);
        result.Status.Should().Be("ReadyToBook");
        (await fixture.Db.CustomerPackages.SingleAsync()).Used.Should().Be(0);
        (await fixture.Db.InstallmentCollections.CountAsync()).Should().Be(0);
    }

    private static QuoteSaleItem Item(string id, string option) =>
        new(id, null, null, null, null, option);

    private sealed class Fixture : IAsyncDisposable
    {
        public TestApplicationDbContext Db { get; }
        public Mock<IBranchScope> BranchScope { get; }
        private readonly string _database;
        private readonly Mock<ICurrentUserService> _user;
        private readonly QuoteSaleHandler _quoteHandler;

        private Fixture(string database, TestApplicationDbContext db, Mock<IBranchScope> branchScope,
            Mock<ICurrentUserService> user)
        {
            _database = database;
            Db = db;
            BranchScope = branchScope;
            _user = user;
            _quoteHandler = new QuoteSaleHandler(db, user.Object, branchScope.Object);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(current => current.TenantId).Returns("tenant-1");
            user.SetupGet(current => current.UserId).Returns("operator");
            var clock = new Mock<IDateTimeProvider>();
            clock.SetupGet(current => current.UtcNow)
                .Returns(new DateTime(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc));
            var branchScope = new Mock<IBranchScope>();
            branchScope.Setup(scope => scope.GetAllowedBranchIdsAsync("operator", It.IsAny<CancellationToken>()))
                .ReturnsAsync(["branch-1"]);
            var database = $"Sale_{Guid.NewGuid()}";
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(database).Options;
            var db = new TestApplicationDbContext(options, clock.Object, user.Object);
            db.Businesses.Add(new Business { Id = "business-1", TenantId = "tenant-1" });
            db.Branches.Add(new Branch
            {
                Id = "branch-1",
                TenantId = "tenant-1",
                BusinessId = "business-1",
                Name = "Main"
            });
            db.Customers.Add(new Customer
            {
                Id = "customer-1",
                TenantId = "tenant-1",
                FullName = "Customer",
                MobileNumber = "01012345678",
                NormalizedMobileNumber = "01012345678"
            });
            db.Packages.AddRange(
                new Package
                {
                    Id = "package",
                    TenantId = "tenant-1",
                    BusinessId = "business-1",
                    Name = "Package",
                    OfferType = OfferType.Package,
                    Status = PackageStatus.Active,
                    CashEnabled = true,
                    InstallmentEnabled = true,
                    Price = 180m,
                    InstallmentPrice = 200m,
                    FirstPayment = 50m,
                    InstallmentCount = 2,
                    SessionCount = 5,
                    SessionDurationMinutes = 60
                },
                new Package
                {
                    Id = "service",
                    TenantId = "tenant-1",
                    BusinessId = "business-1",
                    Name = "Service",
                    OfferType = OfferType.SingleSession,
                    Status = PackageStatus.Active,
                    Price = 30m,
                    SessionDurationMinutes = 30
                });
            db.PackageInstallments.AddRange(
                new PackageInstallment
                {
                    TenantId = "tenant-1",
                    PackageId = "package",
                    Number = 1,
                    Amount = 75m,
                    SessionsUnlocked = 2,
                    CumulativeAvailableSessions = 2
                },
                new PackageInstallment
                {
                    TenantId = "tenant-1",
                    PackageId = "package",
                    Number = 2,
                    Amount = 75m,
                    SessionsUnlocked = 1,
                    CumulativeAvailableSessions = 3
                });
            await db.SaveChangesAsync();
            return new Fixture(database, db, branchScope, user);
        }

        public async Task AddForeignOfferAsync()
        {
            var foreignUser = new Mock<ICurrentUserService>();
            foreignUser.SetupGet(user => user.TenantId).Returns("tenant-2");
            var clock = new Mock<IDateTimeProvider>();
            clock.SetupGet(provider => provider.UtcNow).Returns(DateTime.UtcNow);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(_database).Options;
            await using var foreignDb = new TestApplicationDbContext(options, clock.Object,
                foreignUser.Object);
            foreignDb.Packages.Add(new Package
            {
                Id = "foreign-offer",
                TenantId = "tenant-2",
                BusinessId = "foreign-business",
                Name = "Private offer",
                OfferType = OfferType.SingleSession,
                Status = PackageStatus.Active,
                Price = 50m
            });
            await foreignDb.SaveChangesAsync();
        }

        public Task<SaleQuoteDto> Quote(IReadOnlyList<QuoteSaleItem> items) =>
            _quoteHandler.Handle(new QuoteSaleQuery("customer-1", "branch-1", items), CancellationToken.None);

        public Task<ConfirmSaleResult> Confirm(IReadOnlyList<QuoteSaleItem> items,
            SaleQuoteDto quote, string key, string? method = "cash")
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(x => x.Send(It.IsAny<QuoteSaleQuery>(), It.IsAny<CancellationToken>()))
                .Returns((QuoteSaleQuery query, CancellationToken token) => _quoteHandler.Handle(query, token));
            var handler = new ConfirmSaleHandler(Db, _user.Object, mediator.Object,
                new AuditWriter(Db, _user.Object));
            return handler.Handle(new ConfirmSaleCommand("customer-1", "branch-1", items,
                method, quote.GrandTotal, quote.DueNow, key), CancellationToken.None);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
