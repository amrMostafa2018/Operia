using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Sales.Commands.ConfirmSale;
using Operia.Application.Sales.Commands.CollectSale;
using Operia.Application.Sales.Commands.BookSale;
using Operia.Application.Sales.Queries.GetSale;
using Operia.Application.Revenue.Queries.ListActivityRevenue;
using Operia.Application.Dashboard.Queries.GetDashboardRevenue;
using Operia.Application.Sales.Queries.QuoteSale;
using Operia.Application.Bookings.Commands.CancelBooking;
using Operia.Application.Customers.Queries.ListDirectCustomerSales;
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
    public async Task FailedThenConfirmedCollection_UsesSavedTermsAndCountsOnlyReceivedAmount()
    {
        await using var fixture = await Fixture.CreateAsync();
        var items = new[] { Item("package", "installment"), Item("service", "cash") };
        var quote = await fixture.Quote(items);
        var sale = await fixture.Confirm(items, quote, "collect-1");
        (await fixture.DashboardRevenue()).Today.Should().Be(0m);

        var failed = await fixture.Collect(sale.SaleId, "attempt-1", false);
        failed.AmountCollected.Should().Be(0m);
        (await fixture.Db.CustomerPackages.CountAsync()).Should().Be(0);
        (await fixture.Db.SaleCollections.CountAsync(x => x.Confirmed)).Should().Be(0);
        (await fixture.DashboardRevenue()).Today.Should().Be(0m);

        fixture.Db.Packages.Single(x => x.Id == "package").InstallmentPrice = 999m;
        await fixture.Db.SaveChangesAsync();
        var received = await fixture.Collect(sale.SaleId, "attempt-2", true);
        var repeated = await fixture.Collect(sale.SaleId, "attempt-2", true);
        repeated.CollectionId.Should().Be(received.CollectionId);
        received.AmountCollected.Should().Be(80m);
        var purchases = await fixture.Db.CustomerPackages
            .Where(x => x.SaleId == sale.SaleId).OrderBy(x => x.SaleItemIndex).ToListAsync();
        purchases.Should().HaveCount(2);
        purchases[0].PurchasePrice.Should().Be(200m);
        purchases[0].AmountDue.Should().Be(150m);
        purchases[0].ReleasedSessions.Should().Be(0);
        purchases[0].PurchaseSnapshotJson.Should().Contain("Package");
        purchases[1].PurchasePrice.Should().Be(30m);
        purchases[1].AmountDue.Should().Be(0m);
        (await fixture.Db.AuditLogs.CountAsync(x =>
            x.Action == AuditActions.SaleCollectionAttempted)).Should().Be(1);
        (await fixture.Db.AuditLogs.CountAsync(x =>
            x.Action == AuditActions.SalePaymentConfirmed)).Should().Be(1);
        var revenue = await new ListActivityRevenueHandler(fixture.Db, fixture.User.Object)
            .Handle(new ListActivityRevenueQuery(), CancellationToken.None);
        revenue.ConfirmedTotal.Should().Be(80m);
        revenue.Items.Should().ContainSingle();
        (await fixture.DashboardRevenue()).Today.Should().Be(80m);
    }

    [Fact]
    public async Task CollectionOfUnknownTenantSale_DoesNotCreateAttempt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var action = () => fixture.Collect("foreign-sale", "attempt", true);
        (await action.Should().ThrowAsync<ApiNotFoundException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Sales.NotFound);
        (await fixture.Db.SaleCollections.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SaleRecoveryAndCollection_RejectOtherBranchOperator()
    {
        await using var fixture = await Fixture.CreateAsync();
        var items = new[] { Item("service", "cash") };
        var quote = await fixture.Quote(items);
        var sale = await fixture.Confirm(items, quote, "branch-scope");
        fixture.BranchScope.Setup(x => x.GetAllowedBranchIdsAsync("operator",
                It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var state = () => new GetSaleHandler(fixture.Db, fixture.User.Object,
            fixture.BranchScope.Object).Handle(new GetSaleQuery(sale.SaleId),
            CancellationToken.None);
        (await state.Should().ThrowAsync<ForbiddenException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Sales.BranchUnavailable);
        var collect = () => fixture.Collect(sale.SaleId, "other-branch", true);
        (await collect.Should().ThrowAsync<ForbiddenException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Sales.BranchUnavailable);
        (await fixture.Db.SaleCollections.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task BookNow_ConflictKeepsCollectionAndRetryCreatesOneBooking()
    {
        await using var fixture = await Fixture.CreateAsync();
        var items = new[] { Item("service", "cash") };
        var quote = await fixture.Quote(items);
        var sale = await fixture.Confirm(items, quote, "book-1", disposition: "BookNow");
        await fixture.Collect(sale.SaleId, "book-payment", true);
        fixture.AddBookableEmployee();
        fixture.Db.Bookings.Add(new Booking
        {
            TenantId = "tenant-1",
            BranchId = "branch-1",
            EmployeeId = "staff-1",
            CustomerId = "customer-1",
            ScheduledDate = new DateOnly(2026, 10, 27),
            StartMinutes = 540,
            EndMinutes = 570,
            BookingNumber = "existing",
            IdempotencyKey = "existing"
        });
        await fixture.Db.SaveChangesAsync();

        var conflict = await fixture.Book(sale.SaleId, 540);
        conflict.Status.Should().Be("BookingPending");
        conflict.RecoveryCode.Should().Be(ApiErrorCodes.Bookings.SlotUnavailable);
        (await fixture.Db.SaleCollections.CountAsync(x => x.Confirmed)).Should().Be(1);
        (await fixture.Db.Bookings.CountAsync()).Should().Be(1);

        var booked = await fixture.Book(sale.SaleId, 600);
        booked.Status.Should().Be("Booked");
        var repeated = await fixture.Book(sale.SaleId, 660);
        repeated.BookingId.Should().Be(booked.BookingId);
        (await fixture.Db.Bookings.CountAsync(x => x.SaleId == sale.SaleId)).Should().Be(1);
        (await fixture.Db.CustomerPackages.SingleAsync(x => x.SaleId == sale.SaleId))
            .ReservedSessions.Should().Be(1);
        (await fixture.Db.SaleCollections.CountAsync(x => x.Confirmed)).Should().Be(1);
    }

    [Fact]
    public async Task FailedCollection_DoesNotConfirmBooking_AndRetryKeyCannotDoubleCollect()
    {
        await using var fixture = await Fixture.CreateAsync();
        var items = new[] { Item("service", "cash") };
        var quote = await fixture.Quote(items);
        var sale = await fixture.Confirm(items, quote, "payment-booking", disposition: "BookNow");
        fixture.AddBookableEmployee();
        await fixture.Db.SaveChangesAsync();

        var beforeCollection = () => fixture.Book(sale.SaleId, 600);
        (await beforeCollection.Should().ThrowAsync<ConflictException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Sales.BookingUnavailable);
        await fixture.Collect(sale.SaleId, "failed-key", false);
        var afterFailure = () => fixture.Book(sale.SaleId, 600);
        (await afterFailure.Should().ThrowAsync<ConflictException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Sales.BookingUnavailable);
        (await fixture.Db.Bookings.CountAsync()).Should().Be(0);

        var confirmed = await fixture.Collect(sale.SaleId, "success-key", true);
        var repeated = await fixture.Collect(sale.SaleId, "success-key", true);
        repeated.CollectionId.Should().Be(confirmed.CollectionId);
        (await fixture.Db.SaleCollections.CountAsync(x => x.Confirmed)).Should().Be(1);
        var booked = await fixture.Book(sale.SaleId, 600);
        booked.Status.Should().Be("Booked");
        (await fixture.Db.CustomerPackages.SingleAsync(x => x.SaleId == sale.SaleId)).Used
            .Should().Be(0);
    }

    [Fact]
    public async Task ExtraPackageUnits_ArePurchasesNotAdditionalConcurrentSessions()
    {
        await using var fixture = await Fixture.CreateAsync();
        var items = new[] { new QuoteSaleItem("package", null, null, null, null, "cash", 2) };
        var quote = await fixture.Quote(items);
        var sale = await fixture.Confirm(items, quote, "two-packages", disposition: "BookNow");
        await fixture.Collect(sale.SaleId, "two-packages-paid", true);
        fixture.AddBookableEmployee();
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Book(sale.SaleId, 600, 60);

        result.Status.Should().Be("Booked");
        (await fixture.Db.BookingItems.SingleAsync(x => x.BookingId == result.BookingId))
            .UnitPrice.Should().Be(180m);
        (await fixture.Db.CustomerPackages.CountAsync(x => x.SaleId == sale.SaleId)).Should().Be(2);
        (await fixture.Db.BookingPackageReservations.CountAsync(x => x.BookingId == result.BookingId))
            .Should().Be(1);
        (await fixture.Db.CustomerPackages.Where(x => x.SaleId == sale.SaleId)
            .SumAsync(x => x.Used)).Should().Be(0);
    }

    [Fact]
    public async Task InstallmentFirstPayment_UsesSavedPlanAndKeepsUnreleasedSessionUnbooked()
    {
        await using var fixture = await Fixture.CreateAsync();
        var items = new[] { Item("package", "installment") };
        var quote = await fixture.Quote(items);
        quote.GrandTotal.Should().Be(200m);
        quote.DueNow.Should().Be(50m);
        var sale = await fixture.Confirm(items, quote, "installment-booking", disposition: "BookNow");
        await fixture.Collect(sale.SaleId, "first-payment", true);
        fixture.AddBookableEmployee();
        await fixture.Db.SaveChangesAsync();

        var booking = await fixture.Book(sale.SaleId, 600, 60);

        booking.Status.Should().Be("BookingPending");
        booking.RecoveryCode.Should().Be(ApiErrorCodes.Bookings.PackageSessionUnavailable);
        (await fixture.Db.Bookings.CountAsync(x => x.SaleId == sale.SaleId)).Should().Be(0);
        var purchase = await fixture.Db.CustomerPackages.SingleAsync(x => x.SaleId == sale.SaleId);
        purchase.AmountDue.Should().Be(150m);
        purchase.ReleasedSessions.Should().Be(0);
        (await fixture.Db.SaleCollections.SingleAsync(x => x.SaleId == sale.SaleId))
            .Amount.Should().Be(50m);
    }

    [Fact]
    public async Task SingleSessionInstallment_IsRejectedBeforeSaleOrCollection()
    {
        await using var fixture = await Fixture.CreateAsync();
        var quote = () => fixture.Quote([Item("service", "installment")]);
        (await quote.Should().ThrowAsync<ConflictException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Sales.PaymentOptionUnavailable);
        (await fixture.Db.Sales.CountAsync()).Should().Be(0);
        (await fixture.Db.SaleCollections.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Close_AfterCollectionKeepsSaleWithoutAppointment()
    {
        await using var fixture = await Fixture.CreateAsync();
        var items = new[] { Item("service", "cash") };
        var quote = await fixture.Quote(items);
        var sale = await fixture.Confirm(items, quote, "close-1", disposition: "Close");
        var receipt = await fixture.Collect(sale.SaleId, "close-payment", true);
        receipt.SaleStatus.Should().Be("Closed");
        (await fixture.Db.Sales.SingleAsync(x => x.Id == sale.SaleId)).Status
            .Should().Be("Closed");
        (await fixture.Db.Bookings.CountAsync()).Should().Be(0);
        (await fixture.Db.BookingPackageReservations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DirectServiceSales_ReuseOneLedgerWithoutContactDetails()
    {
        await using var fixture = await Fixture.CreateAsync();
        var items = new[] { Item("service", "cash") };
        var quote = await fixture.Quote(items, direct: true);
        await fixture.Confirm(items, quote, "walk-in-1", direct: true);
        await fixture.Confirm(items, quote, "walk-in-2", direct: true);

        var direct = await fixture.Db.Customers.SingleAsync(customer => customer.IsDirect);
        direct.MobileNumber.Should().BeEmpty();
        direct.NormalizedMobileNumber.Should().BeEmpty();
        (await fixture.Db.Sales.CountAsync(sale => sale.CustomerId == direct.Id)).Should().Be(2);
        (await fixture.Db.Sales.CountAsync(sale => sale.CustomerId == "customer-1")).Should().Be(0);
        var ledger = await new ListDirectCustomerSalesHandler(fixture.Db, fixture.User.Object)
            .Handle(new ListDirectCustomerSalesQuery(), CancellationToken.None);
        ledger!.TotalSales.Should().Be(2);
        ledger.Sales.Should().HaveCount(2);
        ledger.Sales.Should().OnlyContain(sale => sale.Items.Single() == "Service");
    }

    [Fact]
    public async Task DirectServiceBooking_RequiresCollectionAndCreatesNoReusablePurchase()
    {
        await using var fixture = await Fixture.CreateAsync();
        var items = new[] { Item("service", "cash") };
        var quote = await fixture.Quote(items, direct: true);
        var sale = await fixture.Confirm(items, quote, "direct-booking", direct: true,
            disposition: "BookNow");
        fixture.AddBookableEmployee();
        await fixture.Db.SaveChangesAsync();
        var unpaid = () => fixture.Book(sale.SaleId, 600);
        (await unpaid.Should().ThrowAsync<ConflictException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Sales.BookingUnavailable);

        await fixture.Collect(sale.SaleId, "direct-paid", true);
        var booked = await fixture.Book(sale.SaleId, 600);

        booked.Status.Should().Be("Booked");
        var booking = await fixture.Db.Bookings.SingleAsync(x => x.Id == booked.BookingId);
        booking.CustomerMobile.Should().BeEmpty();
        (await fixture.Db.CustomerPackages.CountAsync(x => x.SaleId == sale.SaleId))
            .Should().Be(0);
    }

    [Fact]
    public async Task DirectCustomer_CannotBuyPackageOrUseOwnedSession()
    {
        await using var fixture = await Fixture.CreateAsync();
        var package = () => fixture.Quote([Item("package", "cash")], direct: true);
        (await package.Should().ThrowAsync<ConflictException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Sales.DirectServiceOnly);
        var owned = () => fixture.Quote(
            [new QuoteSaleItem(null, "owned-1", null, null, null, "owned")], direct: true);
        (await owned.Should().ThrowAsync<ConflictException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Sales.DirectServiceOnly);
        (await fixture.Db.Customers.CountAsync(customer => customer.IsDirect)).Should().Be(0);
    }

    [Fact]
    public async Task DirectCustomerLedger_DoesNotExposeAnotherTenantsSales()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddForeignDirectSaleAsync();
        var items = new[] { Item("service", "cash") };
        var quote = await fixture.Quote(items, direct: true);
        await fixture.Confirm(items, quote, "tenant-sale", direct: true);

        var ledger = await new ListDirectCustomerSalesHandler(fixture.Db, fixture.User.Object)
            .Handle(new ListDirectCustomerSalesQuery(), CancellationToken.None);
        ledger!.TotalSales.Should().Be(1);
        ledger.Sales.Single().Items.Should().ContainSingle().Which.Should().Be("Service");
    }

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

    [Fact]
    public async Task CoveredSession_BooksWithoutAnotherChargeOrCompletedUse()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "owned-free-session",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "service",
            PurchasePrice = 30m,
            AmountDue = 0m,
            Total = 1,
            ReleasedSessions = 1,
            IsActive = true
        });
        await fixture.Db.SaveChangesAsync();
        var items = new[] { new QuoteSaleItem(null, "owned-free-session", null, null, null, "owned") };
        var quote = await fixture.Quote(items);
        quote.GrandTotal.Should().Be(0m);
        var sale = await fixture.Confirm(items, quote, "covered-session", null,
            disposition: "BookNow");
        await fixture.Collect(sale.SaleId, "covered-session-zero", true);
        fixture.AddBookableEmployee();
        await fixture.Db.SaveChangesAsync();

        var booked = await fixture.Book(sale.SaleId, 600);

        booked.Status.Should().Be("Booked");
        var purchase = await fixture.Db.CustomerPackages.SingleAsync(x => x.Id == "owned-free-session");
        purchase.Used.Should().Be(0);
        purchase.ReservedSessions.Should().Be(1);
        (await fixture.Db.Bookings.SingleAsync(x => x.Id == booked.BookingId)).PaidAmount
            .Should().Be(0m);
    }

    [Fact]
    public async Task CollectedServiceBooking_CancellationRestoresSessionWithoutUndoingCollection()
    {
        await using var fixture = await Fixture.CreateAsync();
        var items = new[] { Item("service", "cash") };
        var quote = await fixture.Quote(items);
        var sale = await fixture.Confirm(items, quote, "cancel-service", disposition: "BookNow");
        await fixture.Collect(sale.SaleId, "cancel-service-paid", true);
        fixture.AddBookableEmployee();
        await fixture.Db.SaveChangesAsync();
        var booked = await fixture.Book(sale.SaleId, 600);
        var booking = await fixture.Db.Bookings.SingleAsync(x => x.Id == booked.BookingId);
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(
            new DateTime(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc));
        var handler = new CancelBookingHandler(fixture.Db, fixture.User.Object,
            fixture.BranchScope.Object, new AuditWriter(fixture.Db, fixture.User.Object),
            new BookingConcurrencyStore(fixture.Db), clock.Object);

        var cancelled = await handler.Handle(new CancelBookingCommand(
            booking.Id, Convert.ToBase64String(booking.Version)), CancellationToken.None);

        cancelled.Status.Should().Be(nameof(BookingStatus.Cancelled));
        var purchase = await fixture.Db.CustomerPackages.SingleAsync(x => x.SaleId == sale.SaleId);
        purchase.Used.Should().Be(0);
        purchase.ReservedSessions.Should().Be(0);
        (await fixture.Db.SaleCollections.CountAsync(x => x.SaleId == sale.SaleId && x.Confirmed))
            .Should().Be(1);
    }

    private static QuoteSaleItem Item(string id, string option) =>
        new(id, null, null, null, null, option);

    private sealed class Fixture : IAsyncDisposable
    {
        public TestApplicationDbContext Db { get; }
        public Mock<ICurrentUserService> User => _user;
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

        public async Task AddForeignDirectSaleAsync()
        {
            var foreignUser = new Mock<ICurrentUserService>();
            foreignUser.SetupGet(user => user.TenantId).Returns("tenant-2");
            var clock = new Mock<IDateTimeProvider>();
            clock.SetupGet(provider => provider.UtcNow).Returns(DateTime.UtcNow);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(_database).Options;
            await using var foreignDb = new TestApplicationDbContext(options, clock.Object,
                foreignUser.Object);
            foreignDb.Customers.Add(new Customer
            {
                Id = "foreign-direct",
                TenantId = "tenant-2",
                FullName = "عميل مباشر",
                IsDirect = true
            });
            foreignDb.Sales.Add(new Sale
            {
                TenantId = "tenant-2",
                BranchId = "foreign-branch",
                CustomerId = "foreign-direct",
                IdempotencyKey = "foreign-sale",
                ItemsSnapshotJson = "[{\"Name\":\"Private service\"}]"
            });
            await foreignDb.SaveChangesAsync();
        }

        public Task<SaleQuoteDto> Quote(IReadOnlyList<QuoteSaleItem> items, bool direct = false) =>
            _quoteHandler.Handle(new QuoteSaleQuery(direct ? null : "customer-1", "branch-1", items,
                direct), CancellationToken.None);

        public Task<ConfirmSaleResult> Confirm(IReadOnlyList<QuoteSaleItem> items,
            SaleQuoteDto quote, string key, string? method = "cash", bool direct = false,
            string disposition = "Later")
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(x => x.Send(It.IsAny<QuoteSaleQuery>(), It.IsAny<CancellationToken>()))
                .Returns((QuoteSaleQuery query, CancellationToken token) => _quoteHandler.Handle(query, token));
            var handler = new ConfirmSaleHandler(Db, _user.Object, mediator.Object,
                new AuditWriter(Db, _user.Object));
            return handler.Handle(new ConfirmSaleCommand(direct ? null : "customer-1", "branch-1",
                items, method, quote.GrandTotal, quote.DueNow, key, direct, disposition),
                CancellationToken.None);
        }

        public void AddBookableEmployee()
        {
            Db.Employees.Add(new Employee
            {
                Id = "staff-1",
                TenantId = "tenant-1",
                BusinessId = "business-1",
                IdentityUserId = "staff-user",
                IsActive = true
            });
            Db.UserBranches.Add(new UserBranch
            {
                TenantId = "tenant-1",
                BranchId = "branch-1",
                EmployeeId = "staff-1"
            });
            Db.EmployeeWorkingDays.Add(new EmployeeWorkingDay
            {
                TenantId = "tenant-1",
                BranchId = "branch-1",
                EmployeeId = "staff-1",
                Day = "tue",
                Enabled = true,
                FromTime = new TimeOnly(9, 0),
                ToTime = new TimeOnly(17, 0)
            });
        }

        public async Task<BookSaleResult> Book(string saleId, int startMinutes, int durationMinutes = 30)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(_database).Options;
            var identity = new Mock<IIdentityService>();
            identity.Setup(x => x.GetRolesAsync(It.IsAny<IEnumerable<string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, string> { ["staff-user"] = "Staff" });
            var clock = new Mock<IDateTimeProvider>();
            clock.SetupGet(x => x.UtcNow).Returns(
                new DateTime(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc));
            await using var requestDb = new TestApplicationDbContext(
                options, clock.Object, _user.Object);
            var handler = new BookSaleHandler(requestDb, new BookingConcurrencyStore(requestDb),
                _user.Object, BranchScope.Object, identity.Object,
                new AuditWriter(requestDb, _user.Object), clock.Object);
            var result = await handler.Handle(new BookSaleCommand(saleId, "staff-1",
                new DateOnly(2026, 10, 27), startMinutes, startMinutes + durationMinutes),
                CancellationToken.None);
            Db.ChangeTracker.Clear();
            return result;
        }

        public Task<CollectSaleResult> Collect(string saleId, string key, bool confirmed)
        {
            var store = new Mock<ISaleCollectionStore>();
            store.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<CollectSaleResult>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((string _, string _,
                    Func<CancellationToken, Task<CollectSaleResult>> operation,
                    CancellationToken token) => operation(token));
            var clock = new Mock<IDateTimeProvider>();
            clock.SetupGet(x => x.UtcNow).Returns(
                new DateTime(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc));
            var handler = new CollectSaleHandler(Db, store.Object, _user.Object,
                BranchScope.Object,
                new AuditWriter(Db, _user.Object), clock.Object);
            return handler.Handle(new CollectSaleCommand(saleId, key, confirmed),
                CancellationToken.None);
        }

        public Task<DashboardRevenueDto> DashboardRevenue() =>
            new GetDashboardRevenueHandler(Db, _user.Object).Handle(
                new GetDashboardRevenueQuery(
                    new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)),
                CancellationToken.None);

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
