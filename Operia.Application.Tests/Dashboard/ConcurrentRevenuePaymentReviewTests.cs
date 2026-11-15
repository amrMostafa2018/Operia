using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Interfaces;
using Operia.Application.Revenue.Commands.ReviewRevenuePayment;
using Operia.Application.Revenue.Commands.SubmitRevenuePayment;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Dashboard;

public sealed class ConcurrentRevenuePaymentReviewTests
{
    [Fact]
    public async Task ConcurrentConfirmTasks_PersistSingleCollection()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns("tenant-1");
        user.SetupGet(x => x.UserId).Returns("operator");
        var scope = new Mock<IBranchScope>();
        scope.Setup(x => x.GetAllowedBranchIdsAsync("operator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-1"]);
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc));

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ConcurrentReview_{Guid.NewGuid()}").Options;
        await using var db = new TestApplicationDbContext(options, clock.Object, user.Object);
        db.Tenants.Add(new Tenant { Id = "tenant-1", Timezone = "Africa/Cairo" });
        db.Customers.Add(new Customer
        {
            Id = "customer-1",
            TenantId = "tenant-1",
            FullName = "Customer",
            MobileNumber = "01000000001",
            NormalizedMobileNumber = "01000000001"
        });
        db.Branches.Add(new Branch
        {
            Id = "branch-1",
            TenantId = "tenant-1",
            BusinessId = "business-1",
            Name = "Branch 1",
            Address = "Addr",
            PhoneNumber = "01000000000",
            GoogleMapsUrl = "https://maps.example/1",
            Latitude = 0,
            Longitude = 0
        });
        db.Sales.Add(new Sale
        {
            Id = "race-sale",
            TenantId = "tenant-1",
            BranchId = "branch-1",
            CustomerId = "customer-1",
            DueNow = 750m,
            PaymentMethod = "bank_transfer",
            Status = "PendingCollection"
        });
        await db.SaveChangesAsync();

        var store = new SaleCollectionStore(db);
        var audit = new AuditWriter(db, user.Object);
        var pending = await new SubmitRevenuePaymentHandler(
            db, store, user.Object, scope.Object, audit, clock.Object)
            .Handle(new SubmitRevenuePaymentCommand("race-sale", "race-key", null, null),
                CancellationToken.None);

        var review = new ReviewRevenuePaymentHandler(
            db, store, user.Object, scope.Object, audit, clock.Object);
        var confirm = new ReviewRevenuePaymentCommand(pending.PaymentId, true);
        var first = review.Handle(confirm, CancellationToken.None);
        var second = review.Handle(confirm, CancellationToken.None);
        await Task.WhenAll(first, second);
        (await first).Status.Should().Be("Paid");
        (await second).Status.Should().Be("Paid");

        (await db.SaleCollections.CountAsync(x =>
            x.SaleId == "race-sale" && x.Confirmed)).Should().Be(1);
        (await db.AuditLogs.CountAsync(x =>
            x.Action == AuditActions.RevenuePaymentConfirmed &&
            x.EntityId == pending.PaymentId)).Should().Be(1);
    }
}