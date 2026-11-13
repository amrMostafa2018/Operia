using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Interfaces;
using Operia.Application.Dashboard.Queries.GetDashboardSummary;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Dashboard;

public sealed class GetDashboardSummaryTests
{
    private static readonly DateTime TodayUtc =
        new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    private static readonly DateOnly BusinessToday = new(2026, 11, 12);

    [Fact]
    public async Task ReturnsAllCardMetricsInOneQuery()
    {
        var user = User("tenant-1", "operator");
        var branchScope = BranchScope("branch-1");
        var clock = Clock(TodayUtc);
        await using var db = await CreateDb(user.Object, clock.Object);
        db.Customers.Add(Customer("cust-1", "tenant-1", periodsMonthStart: true));
        db.Customers.Add(Customer("cust-walkin", "tenant-1", isDirect: true, periodsMonthStart: true));
        db.Customers.Add(Customer("cust-old", "tenant-1", periodsMonthStart: false));
        db.Bookings.Add(Booking("today", "tenant-1", "branch-1", "employee-1", BusinessToday));
        db.Bookings.Add(Booking(
            "month", "tenant-1", "branch-1", "employee-1", new DateOnly(2026, 11, 1)));
        db.Bookings.Add(Booking(
            "cancelled", "tenant-1", "branch-1", "employee-1", BusinessToday,
            BookingStatus.Cancelled));
        await db.SaveChangesAsync();

        var summary = await new GetDashboardSummaryHandler(
                db, user.Object, branchScope.Object, clock.Object)
            .Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        summary.NewCustomersMonth.Should().Be(1);
        summary.TodayBookings.Should().Be(1);
        summary.MonthlyBookings.Should().Be(2);
        summary.TodayRevenue.Should().Be(0m);
        summary.MonthlyRevenue.Should().Be(0m);
    }

    private static async Task<TestApplicationDbContext> CreateDb(
        ICurrentUserService user, IDateTimeProvider clock)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"DashboardSummary_{Guid.NewGuid()}").Options;
        var db = new TestApplicationDbContext(options, clock, user);
        db.Tenants.Add(new Tenant { Id = "tenant-1", Timezone = "Africa/Cairo" });
        await db.SaveChangesAsync();
        return db;
    }

    private static Mock<ICurrentUserService> User(string tenantId, string userId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns(tenantId);
        user.SetupGet(x => x.UserId).Returns(userId);
        return user;
    }

    private static Mock<IDateTimeProvider> Clock(DateTime utcNow)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(utcNow);
        return clock;
    }

    private static Mock<IBranchScope> BranchScope(params string[] branchIds)
    {
        var branchScope = new Mock<IBranchScope>();
        branchScope
            .Setup(x => x.GetAllowedBranchIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(branchIds);
        return branchScope;
    }

    private static Customer Customer(
        string id,
        string tenantId,
        bool isDirect = false,
        bool periodsMonthStart = true) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            FullName = id,
            MobileNumber = "01000000001",
            NormalizedMobileNumber = "01000000001",
            IsDirect = isDirect,
            CreatedAt = periodsMonthStart
                ? new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc)
                : new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc),
        };

    private static Booking Booking(
        string id,
        string tenantId,
        string branchId,
        string employeeId,
        DateOnly scheduledDate,
        BookingStatus status = BookingStatus.Booked) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            BookingNumber = id,
            IdempotencyKey = id,
            BranchId = branchId,
            EmployeeId = employeeId,
            CustomerId = "customer-1",
            CustomerName = id,
            CustomerMobile = "01000000000",
            ScheduledDate = scheduledDate,
            StartMinutes = 600,
            EndMinutes = 660,
            Status = status
        };
}
