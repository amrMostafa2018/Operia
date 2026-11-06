using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Interfaces;
using Operia.Application.Customers.Queries.GetCustomerActionHistory;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Customers;

public sealed class HistoricalAuditHistoryTests
{
    [Fact]
    public async Task NoSaleHistoryRecoversOnlyValuesProvenBySavedAuditDetails()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc));
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns("tenant-1");
        user.SetupGet(x => x.UserId).Returns("operator-1");
        var scope = new Mock<IBranchScope>();
        scope.Setup(x => x.GetAllowedBranchIdsAsync("operator-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-1"]);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"HistoricalHistory_{Guid.NewGuid()}").Options;
        await using var db = new TestApplicationDbContext(options, clock.Object, user.Object);
        db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "purchase-1",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "offer-1",
            BranchId = "branch-1"
        });
        db.AuditLogs.AddRange(
            new AuditLog
            {
                Id = "audit-1",
                TenantId = "tenant-1",
                EntityType = nameof(CustomerPackage),
                EntityId = "purchase-1",
                EntityName = "Purchase",
                Action = AuditActions.PackageInstallmentCollected,
                DetailsJson = "{\"amount\":5000,\"amountDue\":15000}"
            },
            new AuditLog
            {
                Id = "audit-2",
                TenantId = "tenant-1",
                EntityType = nameof(CustomerPackage),
                EntityId = "purchase-1",
                EntityName = "Purchase",
                Action = AuditActions.PurchaseCancelled
            },
            new AuditLog
            {
                Id = "audit-3",
                TenantId = "tenant-1",
                EntityType = nameof(CustomerPackage),
                EntityId = "purchase-1",
                EntityName = "Purchase",
                Action = AuditActions.PurchaseBranchAttributed,
                DetailsJson = "{\"oldValue\":null,\"newValue\":\"branch-1\"}"
            });
        await db.SaveChangesAsync();
        var handler = new GetCustomerActionHistoryHandler(db, user.Object, scope.Object);

        var page = await handler.Handle(new GetCustomerActionHistoryQuery(
            "customer-1", "purchase-1"), CancellationToken.None);
        page.Items.Single(x => x.Action == AuditActions.PackageInstallmentCollected)
            .Should().Match<CustomerActionHistoryRow>(x =>
                x.OldValue == "20000" && x.NewValue == "15000");
        page.Items.Single(x => x.Action == AuditActions.PurchaseCancelled)
            .Should().Match<CustomerActionHistoryRow>(x =>
                x.OldValue == null && x.NewValue == null);
        page.Items.Single(x => x.Action == AuditActions.PurchaseBranchAttributed)
            .NewValue.Should().Be("branch-1");

        scope.Setup(x => x.GetAllowedBranchIdsAsync("operator-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-2"]);
        var denied = () => handler.Handle(new GetCustomerActionHistoryQuery(
            "customer-1", "purchase-1"), CancellationToken.None);
        await denied.Should().ThrowAsync<Operia.Application.Common.Exceptions.ApiNotFoundException>();
    }
}
