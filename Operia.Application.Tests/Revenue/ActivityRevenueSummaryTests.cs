using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Dashboard.Queries.GetDashboardRevenue;
using Operia.Application.Revenue.Queries.GetActivityRevenueSummary;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Revenue;

public sealed class ActivityRevenueSummaryTests
{
    private static readonly DateTime TodayFrom = new(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime TodayTo = TodayFrom.AddDays(1);
    private static readonly DateTime MonthFrom = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MonthTo = MonthFrom.AddMonths(1);

    [Fact]
    public async Task CardsCountConfirmedMovementsAndSubtractRefundOnlyFromNet()
    {
        await using var fixture = await Fixture.CreateAsync();
        var summary = await fixture.Summary();
        summary.Today.TransactionCount.Should().Be(1);
        summary.Today.GrossCollected.Should().Be(5000m);
        summary.Today.Refunds.Should().Be(1500m);
        summary.Today.NetCollected.Should().Be(3500m);
        summary.Month.Should().BeEquivalentTo(summary.Today);

        var dashboard = await new GetDashboardRevenueHandler(fixture.Db,
            fixture.User.Object, fixture.Scope.Object).Handle(
                new GetDashboardRevenueQuery(TodayFrom, TodayTo, MonthFrom, MonthTo),
                CancellationToken.None);
        dashboard.Today.Should().Be(summary.Today.NetCollected);
        dashboard.Month.Should().Be(summary.Month.NetCollected);
    }

    [Fact]
    public async Task BranchScopeAppliesBeforeAggregationAndRejectsOtherBranch()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Scope.Setup(x => x.GetAllowedBranchIdsAsync("operator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-1", "branch-2"]);
        (await fixture.Summary()).Today.GrossCollected.Should().Be(12000m);
        (await fixture.Summary("branch-1")).Today.GrossCollected.Should().Be(5000m);
        fixture.Scope.Setup(x => x.GetAllowedBranchIdsAsync("operator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-1"]);
        (await fixture.Summary()).Today.GrossCollected.Should().Be(5000m);
        var otherBranch = () => fixture.Summary("branch-2");
        await otherBranch.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task NumberedInstallmentCountsOnceInItsCollectionPeriod()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.InstallmentCollections.Add(new InstallmentCollection
        {
            TenantId = "tenant-1",
            CustomerPackageId = "purchase-old",
            InstallmentNumber = 1,
            Amount = 2000m,
            ConfirmedAtUtc = TodayFrom.AddHours(13)
        });
        await fixture.Db.SaveChangesAsync();

        var summary = await fixture.Summary();
        summary.Today.TransactionCount.Should().Be(2);
        summary.Today.GrossCollected.Should().Be(7000m);
        summary.Today.NetCollected.Should().Be(5500m);
    }

    [Fact]
    public async Task AnotherTenantCannotReadCollectionsEvenWithBranchId()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.User.SetupGet(x => x.TenantId).Returns("tenant-2");
        var summary = await fixture.Summary("branch-1");
        summary.Today.TransactionCount.Should().Be(0);
        summary.Today.GrossCollected.Should().Be(0m);
        summary.Today.Refunds.Should().Be(0m);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public TestApplicationDbContext Db { get; }
        public Mock<ICurrentUserService> User { get; }
        public Mock<IBranchScope> Scope { get; }

        private Fixture(TestApplicationDbContext db, Mock<ICurrentUserService> user,
            Mock<IBranchScope> scope)
        {
            Db = db;
            User = user;
            Scope = scope;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.TenantId).Returns("tenant-1");
            user.SetupGet(x => x.UserId).Returns("operator");
            var scope = new Mock<IBranchScope>();
            scope.Setup(x => x.GetAllowedBranchIdsAsync("operator", It.IsAny<CancellationToken>()))
                .ReturnsAsync(["branch-1"]);
            var clock = new Mock<IDateTimeProvider>();
            clock.SetupGet(x => x.UtcNow).Returns(TodayFrom.AddHours(12));
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"RevenueSummary_{Guid.NewGuid()}").Options;
            var db = new TestApplicationDbContext(options, clock.Object, user.Object);
            db.Branches.Add(new Branch { Id = "branch-1", TenantId = "tenant-1", Name = "Main" });
            db.Branches.Add(new Branch { Id = "branch-2", TenantId = "tenant-1", Name = "Second" });
            db.Customers.Add(new Customer
            {
                Id = "customer-1",
                TenantId = "tenant-1",
                FullName = "Customer",
                MobileNumber = "01012345678",
                NormalizedMobileNumber = "01012345678"
            });
            db.Packages.Add(new Package { Id = "offer-1", TenantId = "tenant-1", Name = "Offer" });
            db.Sales.Add(new Sale
            {
                Id = "sale-old",
                TenantId = "tenant-1",
                BranchId = "branch-1",
                CustomerId = "customer-1"
            });
            db.Sales.Add(new Sale
            {
                Id = "sale-today",
                TenantId = "tenant-1",
                BranchId = "branch-1",
                CustomerId = "customer-1"
            });
            db.Sales.Add(new Sale
            {
                Id = "sale-other-branch",
                TenantId = "tenant-1",
                BranchId = "branch-2",
                CustomerId = "customer-1"
            });
            db.CustomerPackages.Add(new CustomerPackage
            {
                Id = "purchase-old",
                TenantId = "tenant-1",
                CustomerId = "customer-1",
                PackageId = "offer-1",
                SaleId = "sale-old",
                Total = 1
            });
            db.SaleCollections.AddRange(
                new SaleCollection
                {
                    TenantId = "tenant-1",
                    SaleId = "sale-old",
                    Amount = 5000m,
                    Confirmed = true,
                    AttemptedAtUtc = new DateTime(2026, 9, 20, 12, 0, 0,
                        DateTimeKind.Utc)
                },
                new SaleCollection
                {
                    TenantId = "tenant-1",
                    SaleId = "sale-today",
                    Amount = 5000m,
                    Confirmed = true,
                    AttemptedAtUtc = TodayFrom.AddHours(9)
                },
                new SaleCollection
                {
                    TenantId = "tenant-1",
                    SaleId = "sale-today",
                    Amount = 900m,
                    Confirmed = false,
                    AttemptedAtUtc = TodayFrom.AddHours(10)
                },
                new SaleCollection
                {
                    TenantId = "tenant-1",
                    SaleId = "sale-other-branch",
                    Amount = 7000m,
                    Confirmed = true,
                    AttemptedAtUtc = TodayFrom.AddHours(10)
                });
            db.RefundTransactions.Add(new RefundTransaction
            {
                TenantId = "tenant-1",
                SaleId = "sale-old",
                CustomerPackageId = "purchase-old",
                CustomerId = "customer-1",
                Amount = 1500m,
                RecordedAtUtc = TodayFrom.AddHours(11)
            });
            await db.SaveChangesAsync();
            return new Fixture(db, user, scope);
        }

        public Task<ActivityRevenueSummaryDto> Summary(string? branchId = null) =>
            new GetActivityRevenueSummaryHandler(Db, User.Object, Scope.Object).Handle(
                new GetActivityRevenueSummaryQuery(TodayFrom, TodayTo, MonthFrom, MonthTo,
                    branchId), CancellationToken.None);

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
