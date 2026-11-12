using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Interfaces;
using Operia.Application.Dashboard.Queries.GetDashboardNewCustomers;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Dashboard;

public sealed class GetDashboardNewCustomersTests
{
    private static readonly DateTime ThisMonth =
        new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime LastMonth =
        new(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CountsRegisteredCustomersCreatedInTheCurrentBusinessMonth()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns("tenant-1");
        user.SetupGet(x => x.UserId).Returns("operator");
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(LastMonth);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"NewCustomers_{Guid.NewGuid()}").Options;
        await using var db = new TestApplicationDbContext(options, clock.Object, user.Object);
        db.Tenants.Add(new Tenant { Id = "tenant-1", Timezone = "Africa/Cairo" });
        db.Tenants.Add(new Tenant { Id = "tenant-2", Timezone = "Africa/Cairo" });
        db.Customers.Add(Customer("last-month", "tenant-1", "01000000001"));
        await db.SaveChangesAsync();

        clock.SetupGet(x => x.UtcNow).Returns(ThisMonth);
        db.Customers.Add(Customer("this-month", "tenant-1", "01000000002"));
        db.Customers.Add(Customer("blocked", "tenant-1", "01000000003", blocked: true));
        db.Customers.Add(Customer("direct", "tenant-1", "01000000004", direct: true));
        await db.SaveChangesAsync();

        user.SetupGet(x => x.TenantId).Returns("tenant-2");
        db.Customers.Add(Customer("other-tenant", "tenant-2", "01000000005"));
        await db.SaveChangesAsync();
        user.SetupGet(x => x.TenantId).Returns("tenant-1");

        var count = await new GetDashboardNewCustomersHandler(db, user.Object, clock.Object)
            .Handle(new GetDashboardNewCustomersQuery(), CancellationToken.None);

        count.Count.Should().Be(2);
    }

    private static Customer Customer(
        string id, string tenantId, string mobile, bool direct = false, bool blocked = false) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            FullName = id,
            MobileNumber = mobile,
            NormalizedMobileNumber = mobile,
            IsDirect = direct,
            IsBlocked = blocked
        };
}
