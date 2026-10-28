using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Customers.Commands.CancelAndRefundPurchase;
using Operia.Application.Customers.Commands.SetCustomerBlock;
using Operia.Application.Customers.Queries.GetCustomerAction;
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

        await handler.Handle(new SetCustomerBlockCommand("customer-1", false, null,
            Convert.ToBase64String(customer.Version)), CancellationToken.None);
        (await fixture.Db.AuditLogs.CountAsync(x => x.Action == AuditActions.CustomerBlocked))
            .Should().Be(1);
        (await fixture.Db.AuditLogs.CountAsync(x => x.Action == AuditActions.CustomerUnblocked))
            .Should().Be(1);
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

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
