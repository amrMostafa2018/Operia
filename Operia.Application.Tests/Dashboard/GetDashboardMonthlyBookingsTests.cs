using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Interfaces;
using Operia.Application.Dashboard.Queries.GetDashboardMonthlyBookings;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Dashboard;

public sealed class GetDashboardMonthlyBookingsTests
{
    private static readonly DateTime ThisMonth =
        new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CountsBookedAndCompletedAppointmentsScheduledThisBusinessMonth()
    {
        var user = User("tenant-1", "operator");
        var branchScope = BranchScope("branch-1");
        var clock = Clock(ThisMonth);
        await using var db = await CreateDb(user.Object, clock.Object);
        db.Bookings.Add(Booking("this-month", "tenant-1", "branch-1", "employee-1", new DateOnly(2026, 11, 12)));
        db.Bookings.Add(Booking(
            "completed", "tenant-1", "branch-1", "employee-1", new DateOnly(2026, 11, 1),
            BookingStatus.Completed));
        db.Bookings.Add(Booking(
            "cancelled", "tenant-1", "branch-1", "employee-1", new DateOnly(2026, 11, 20),
            BookingStatus.Cancelled));
        db.Bookings.Add(Booking("last-month", "tenant-1", "branch-1", "employee-1", new DateOnly(2026, 10, 31)));
        db.Bookings.Add(Booking("next-month", "tenant-1", "branch-1", "employee-1", new DateOnly(2026, 12, 1)));
        db.Bookings.Add(Booking("other-branch", "tenant-1", "branch-2", "employee-1", new DateOnly(2026, 11, 15)));
        await db.SaveChangesAsync();

        user.SetupGet(x => x.TenantId).Returns("tenant-2");
        db.Bookings.Add(Booking("other-tenant", "tenant-2", "branch-1", "employee-9", new DateOnly(2026, 11, 12)));
        await db.SaveChangesAsync();
        user.SetupGet(x => x.TenantId).Returns("tenant-1");

        var count = await new GetDashboardMonthlyBookingsHandler(
                db, user.Object, branchScope.Object, clock.Object)
            .Handle(new GetDashboardMonthlyBookingsQuery(), CancellationToken.None);

        count.Count.Should().Be(2);
    }

    [Fact]
    public async Task StaffCountsOnlyAppointmentsAssignedToTheirEmployee()
    {
        var user = User("tenant-1", "staff-user");
        user.Setup(x => x.IsInRole(Roles.Staff)).Returns(true);
        var branchScope = BranchScope("branch-1");
        var clock = Clock(ThisMonth);
        await using var db = await CreateDb(user.Object, clock.Object);
        db.Employees.Add(Employee("employee-1", "tenant-1", "staff-user"));
        db.Employees.Add(Employee("employee-2", "tenant-1", "other-user"));
        db.Bookings.Add(Booking("mine", "tenant-1", "branch-1", "employee-1", new DateOnly(2026, 11, 12)));
        db.Bookings.Add(Booking("theirs", "tenant-1", "branch-1", "employee-2", new DateOnly(2026, 11, 13)));
        await db.SaveChangesAsync();

        var count = await new GetDashboardMonthlyBookingsHandler(
                db, user.Object, branchScope.Object, clock.Object)
            .Handle(new GetDashboardMonthlyBookingsQuery(), CancellationToken.None);

        count.Count.Should().Be(1);
    }

    private static async Task<TestApplicationDbContext> CreateDb(
        ICurrentUserService user, IDateTimeProvider clock)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"MonthlyBookings_{Guid.NewGuid()}").Options;
        var db = new TestApplicationDbContext(options, clock, user);
        db.Tenants.Add(new Tenant { Id = "tenant-1", Timezone = "Africa/Cairo" });
        db.Tenants.Add(new Tenant { Id = "tenant-2", Timezone = "Africa/Cairo" });
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

    private static Employee Employee(string id, string tenantId, string identityUserId) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            BusinessId = "business-1",
            IdentityUserId = identityUserId,
            Code = id,
            FullName = id,
            JoiningDate = new DateOnly(2026, 1, 1)
        };
}
