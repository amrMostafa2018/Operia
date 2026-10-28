using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Customers.Commands.CancelAndRefundPurchase;
using Operia.Application.Customers.Commands.SetCustomerBlock;
using Operia.Application.Customers.Queries.GetCustomerAction;
using Operia.Application.Customers.Queries.GetCustomerActionHistory;
using Operia.Application.Dashboard.Queries.GetDashboardRevenue;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Customers;

public sealed class CustomerRefundAndBlockTests
{
    [Fact]
    public async Task RefundUsesPaidCap_AndCancelsFutureBookingOnlyOnce()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.Refund(1500m, "refund-one");
        first.RemainingRefundable.Should().Be(3500m);
        (await fixture.Refund(1500m, "refund-one")).RefundId.Should().Be(first.RefundId);
        var excess = () => fixture.Refund(3501m, "refund-two");
        (await excess.Should().ThrowAsync<ConflictException>()).Which.ErrorCode
            .Should().Be(ApiErrorCodes.Customers.RefundExceedsPaid);

        var purchase = await fixture.Db.CustomerPackages.SingleAsync();
        purchase.IsActive.Should().BeFalse();
        purchase.Used.Should().Be(3);
        purchase.ReservedSessionCount.Should().Be(0);
        (await fixture.Db.Bookings.SingleAsync()).Status.Should().Be(BookingStatus.Cancelled);
        (await fixture.Db.SaleCollections.SingleAsync()).Amount.Should().Be(5000m);
        (await fixture.Db.RefundTransactions.CountAsync()).Should().Be(1);
        (await fixture.Db.AuditLogs.CountAsync(x => x.Action == AuditActions.PurchaseCancelled))
            .Should().Be(1);

        var second = await fixture.Refund(3500m, "refund-two");
        second.RemainingRefundable.Should().Be(0m);
        (await fixture.Db.RefundTransactions.SumAsync(x => x.Amount)).Should().Be(5000m);
        (await fixture.Db.AuditLogs.CountAsync(x => x.Action == AuditActions.RefundPosted))
            .Should().Be(2);

        var revenue = await new GetDashboardRevenueHandler(fixture.Db, fixture.User.Object)
            .Handle(new GetDashboardRevenueQuery(
                new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 29, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)), CancellationToken.None);
        revenue.Today.Should().Be(0m);
    }

    [Fact]
    public async Task BlockStateIsSeparateFromPurchase_AndAuditsBothTransitions()
    {
        await using var fixture = await Fixture.CreateAsync();
        var action = await new GetCustomerActionHandler(fixture.Db, fixture.User.Object,
            fixture.Scope.Object).Handle(new GetCustomerActionQuery("customer-1", "purchase-1"),
                CancellationToken.None);
        action.RemainingRefundable.Should().Be(5000m);
        var handler = new SetCustomerBlockHandler(fixture.Db, fixture.User.Object,
            new AuditWriter(fixture.Db, fixture.User.Object));
        await handler.Handle(new SetCustomerBlockCommand("customer-1", true, "No online booking",
            action.CustomerVersion), CancellationToken.None);
        var customer = await fixture.Db.Customers.SingleAsync();
        customer.IsBlocked.Should().BeTrue();
        customer.BlockReason.Should().Be("No online booking");
        (await fixture.Db.Bookings.SingleAsync()).Status.Should().Be(BookingStatus.Booked);
        (await fixture.Db.CustomerPackages.SingleAsync()).IsActive.Should().BeTrue();

        await handler.Handle(new SetCustomerBlockCommand("customer-1", true, "Updated reason",
            Convert.ToBase64String(customer.Version)), CancellationToken.None);

        await handler.Handle(new SetCustomerBlockCommand("customer-1", false, null,
            Convert.ToBase64String(customer.Version)), CancellationToken.None);
        (await fixture.Db.AuditLogs.CountAsync(x => x.Action == AuditActions.CustomerBlocked))
            .Should().Be(1);
        (await fixture.Db.AuditLogs.CountAsync(x => x.Action == AuditActions.CustomerUnblocked))
            .Should().Be(1);
        (await fixture.Db.AuditLogs.CountAsync(x =>
            x.Action == AuditActions.CustomerBlockReasonChanged)).Should().Be(1);
    }

    [Fact]
    public async Task HistoryShowsSavedRefundAndBlockTransitionsForSelectedCustomer()
    {
        await using var fixture = await Fixture.CreateAsync();
        var action = await new GetCustomerActionHandler(fixture.Db, fixture.User.Object,
            fixture.Scope.Object).Handle(new GetCustomerActionQuery("customer-1", "purchase-1"),
                CancellationToken.None);
        var block = new SetCustomerBlockHandler(fixture.Db, fixture.User.Object,
            new AuditWriter(fixture.Db, fixture.User.Object));
        await block.Handle(new SetCustomerBlockCommand("customer-1", true, "Safety review",
            action.CustomerVersion), CancellationToken.None);
        var customer = await fixture.Db.Customers.SingleAsync();
        await block.Handle(new SetCustomerBlockCommand("customer-1", false, null,
            Convert.ToBase64String(customer.Version)), CancellationToken.None);
        var refund = await fixture.Refund(1500m, "refund-one");

        var history = await fixture.History();
        history.Items.Should().Contain(x => x.Action == AuditActions.CustomerBlocked &&
            x.OldValue == "unblocked" && x.NewValue == "blocked" &&
            x.NewReason == "Safety review" && x.ReferenceId == "customer-1");
        history.Items.Should().Contain(x => x.Action == AuditActions.CustomerUnblocked &&
            x.OldValue == "blocked" && x.NewValue == "unblocked" &&
            x.OldReason == "Safety review" && x.NewReason == null);
        history.Items.Should().Contain(x => x.Action == AuditActions.RefundPosted &&
            x.Amount == 1500m && x.ReferenceId == refund.RefundId &&
            x.ReferenceType == "refund");
        history.Items.Should().Contain(x => x.Action == AuditActions.PurchaseCancelled &&
            x.OldValue == "active" && x.NewValue == "cancelled");
    }

    [Fact]
    public async Task HistoryCursorDoesNotRepeatRowsWhenNewAuditArrives()
    {
        await using var fixture = await Fixture.CreateAsync();
        for (var index = 0; index < 12; index++)
        {
            fixture.Db.AuditLogs.Add(new AuditLog
            {
                Id = $"history-{index:D2}",
                TenantId = "tenant-1",
                EntityType = nameof(Customer),
                EntityId = "customer-1",
                Action = AuditActions.CustomerBlocked
            });
        }
        fixture.Db.AuditLogs.Add(new AuditLog
        {
            Id = "other-customer",
            TenantId = "tenant-1",
            EntityType = nameof(Customer),
            EntityId = "other-customer",
            Action = AuditActions.CustomerBlocked
        });
        await fixture.Db.SaveChangesAsync();

        var first = await fixture.History();
        first.Items.Should().HaveCount(10);
        first.NextCursor.Should().NotBeNull();
        fixture.Db.AuditLogs.Add(new AuditLog
        {
            Id = "history-99",
            TenantId = "tenant-1",
            EntityType = nameof(Customer),
            EntityId = "customer-1",
            Action = AuditActions.CustomerBlocked
        });
        await fixture.Db.SaveChangesAsync();

        var second = await fixture.History(first.NextCursor);
        second.Items.Should().HaveCount(2);
        second.NextCursor.Should().BeNull();
        first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id))
            .Should().BeEmpty();
        first.Items.Concat(second.Items).Select(x => x.Id).Should().NotContain("other-customer");
    }

    [Fact]
    public async Task HistoryRequiresSelectedSaleBranch()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Scope.Setup(x => x.GetAllowedBranchIdsAsync("admin-1",
                It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var read = () => fixture.History();
        await read.Should().ThrowAsync<ApiNotFoundException>();
    }

    [Fact]
    public async Task SaleLineHistoryShowsConfirmedPaymentAndRejectsAnotherCustomer()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.AuditLogs.Add(new AuditLog
        {
            TenantId = "tenant-1",
            EntityType = nameof(Sale),
            EntityId = "sale-1",
            Action = AuditActions.SalePaymentConfirmed,
            UserDisplayName = "Cashier",
            DetailsJson = """
                {"id":"payment-1","amount":5000,"confirmed":true,
                 "oldStatus":"PendingCollection","newStatus":"ReadyToBook"}
                """
        });
        await fixture.Db.SaveChangesAsync();
        var handler = new GetCustomerActionHistoryHandler(fixture.Db,
            fixture.User.Object, fixture.Scope.Object);

        var history = await handler.Handle(new GetCustomerActionHistoryQuery(
            "customer-1", "sale-1", true), CancellationToken.None);
        history.Items.Should().ContainSingle(x => x.Action == AuditActions.SalePaymentConfirmed &&
            x.Actor == "Cashier" && x.Amount == 5000m &&
            x.OldValue == "PendingCollection" && x.NewValue == "ReadyToBook" &&
            x.ReferenceType == "payment" && x.ReferenceId == "payment-1");

        var otherCustomer = () => handler.Handle(new GetCustomerActionHistoryQuery(
            "customer-2", "sale-1", true), CancellationToken.None);
        await otherCustomer.Should().ThrowAsync<ApiNotFoundException>();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public TestApplicationDbContext Db { get; }
        public Mock<ICurrentUserService> User { get; }
        public Mock<IBranchScope> Scope { get; }
        private readonly Mock<IDateTimeProvider> _clock;

        private Fixture(TestApplicationDbContext db, Mock<ICurrentUserService> user,
            Mock<IBranchScope> scope, Mock<IDateTimeProvider> clock)
        {
            Db = db;
            User = user;
            Scope = scope;
            _clock = clock;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.TenantId).Returns("tenant-1");
            user.SetupGet(x => x.UserId).Returns("admin-1");
            var scope = new Mock<IBranchScope>();
            scope.Setup(x => x.GetAllowedBranchIdsAsync("admin-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(["branch-1"]);
            var clock = new Mock<IDateTimeProvider>();
            clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc));
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"CustomerRefund_{Guid.NewGuid()}").Options;
            var db = new TestApplicationDbContext(options, clock.Object, user.Object);
            db.Branches.Add(new Branch { Id = "branch-1", TenantId = "tenant-1", Name = "Branch" });
            db.Customers.Add(new Customer
            {
                Id = "customer-1",
                TenantId = "tenant-1",
                FullName = "Customer",
                MobileNumber = "01012345678",
                NormalizedMobileNumber = "01012345678"
            });
            db.Packages.Add(new Package
            {
                Id = "offer-1",
                TenantId = "tenant-1",
                Name = "Offer",
                OfferType = OfferType.Package,
                SessionCount = 5,
                Status = PackageStatus.Active
            });
            db.Sales.Add(new Sale
            {
                Id = "sale-1",
                TenantId = "tenant-1",
                BranchId = "branch-1",
                CustomerId = "customer-1",
                Status = "ReadyToBook"
            });
            db.SaleCollections.Add(new SaleCollection
            {
                TenantId = "tenant-1",
                SaleId = "sale-1",
                IdempotencyKey = "collection-1",
                Amount = 5000m,
                Confirmed = true,
                AttemptedAtUtc = clock.Object.UtcNow
            });
            db.CustomerPackages.Add(new CustomerPackage
            {
                Id = "purchase-1",
                TenantId = "tenant-1",
                CustomerId = "customer-1",
                PackageId = "offer-1",
                SaleId = "sale-1",
                PurchasePrice = 5000m,
                AmountDue = 0m,
                Total = 5,
                Used = 3,
                ReservedSessions = 1,
                IsActive = true
            });
            db.Bookings.Add(new Booking
            {
                Id = "booking-1",
                TenantId = "tenant-1",
                BranchId = "branch-1",
                EmployeeId = "staff-1",
                CustomerId = "customer-1",
                ScheduledDate = new DateOnly(2026, 10, 30),
                Status = BookingStatus.Booked
            });
            db.BookingPackageReservations.Add(new BookingPackageReservation
            {
                TenantId = "tenant-1",
                BookingId = "booking-1",
                CustomerPackageId = "purchase-1",
                SessionNumber = 4
            });
            await db.SaveChangesAsync();
            return new(db, user, scope, clock);
        }

        public Task<CancelAndRefundPurchaseResult> Refund(decimal amount, string key)
        {
            var store = new Mock<ICustomerRefundStore>();
            store.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<CancelAndRefundPurchaseResult>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((string _, string _,
                    Func<CancellationToken, Task<CancelAndRefundPurchaseResult>> action,
                    CancellationToken token) => action(token));
            var handler = new CancelAndRefundPurchaseHandler(Db, store.Object, User.Object,
                Scope.Object, new AuditWriter(Db, User.Object), _clock.Object);
            return handler.Handle(new CancelAndRefundPurchaseCommand("purchase-1", amount, key, null),
                CancellationToken.None);
        }

        public Task<CustomerActionHistoryPage> History(string? cursor = null)
        {
            var handler = new GetCustomerActionHistoryHandler(Db, User.Object, Scope.Object);
            return handler.Handle(new GetCustomerActionHistoryQuery(
                "customer-1", "purchase-1", false, cursor), CancellationToken.None);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
