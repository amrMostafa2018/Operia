using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Customers.Queries.GetCustomerPurchaseDetails;
using Operia.Application.Customers.Queries.GetCustomerStatement;
using Operia.Application.Packages;
using Operia.Application.Sales.Commands.ConfirmSale;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Customers;

public sealed class CustomerStatementTests
{
    [Fact]
    public async Task HistoricalStatementUsesSavedPlanAndProvenBranchWithoutSale()
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
            .UseInMemoryDatabase($"HistoricalStatement_{Guid.NewGuid()}").Options;
        await using var db = new TestApplicationDbContext(options, clock.Object, user.Object);
        db.Customers.Add(new Customer
        {
            Id = "customer-1",
            TenantId = "tenant-1",
            FullName = "Historical customer"
        });
        db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "purchase-1",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "offer-1",
            BranchId = "branch-1",
            PurchasePrice = 20000m,
            AmountDue = 15000m,
            Total = 3
        });
        db.CustomerPackageInstallments.AddRange(
            new CustomerPackageInstallment
            {
                TenantId = "tenant-1",
                CustomerPackageId = "purchase-1",
                Number = 1,
                Amount = 7500m
            },
            new CustomerPackageInstallment
            {
                TenantId = "tenant-1",
                CustomerPackageId = "purchase-1",
                Number = 2,
                Amount = 7500m
            });
        await db.SaveChangesAsync();
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<GetCustomerPurchaseDetailsQuery>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new CustomerPurchaseDetailsDto(
                "purchase-1", clock.Object.UtcNow, null, null, null, "active", "partial",
                new PurchaseBalanceDto(3, 0, 3), null, 20000m, 5000m, 15000m,
                1, 2, [], [], true, 7500m, [], []));
        var handler = new GetCustomerStatementHandler(db, user.Object, scope.Object, mediator.Object);

        var result = await handler.Handle(new GetCustomerStatementQuery(
            "customer-1", "purchase-1"), CancellationToken.None);
        result.OperationCount.Should().Be(1);
        result.Purchase.AmountPaid.Should().Be(5000m);
        result.Installments.Should().HaveCount(2);
        result.Installments[0].Status.Should().Be("due");
        result.Installments[0].DueOn.Should().BeNull();

        scope.Setup(x => x.GetAllowedBranchIdsAsync("operator-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-2"]);
        var denied = () => handler.Handle(new GetCustomerStatementQuery(
            "customer-1", "purchase-1"), CancellationToken.None);
        await denied.Should().ThrowAsync<ApiNotFoundException>();
    }

    [Fact]
    public async Task SelectedPurchaseUsesItsOwnPlanAndCollectionsWithinAllowedBranch()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc));
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns("tenant-1");
        user.SetupGet(x => x.UserId).Returns("operator-1");
        var scope = new Mock<IBranchScope>();
        scope.Setup(x => x.GetAllowedBranchIdsAsync("operator-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-1"]);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"Statement_{Guid.NewGuid()}").Options;
        await using var db = new TestApplicationDbContext(options, clock.Object, user.Object);
        var snapshot = new PackagePurchaseSnapshot("Original name", OfferType.Package,
            60, 3, 2500, null, "category-1", null, "installment", 20000m,
            20000m, null, 5000m,
            [new PurchaseInstallmentSnapshot(1, 5000m, 0, 0),
             new PurchaseInstallmentSnapshot(2, 5000m, 0, 0),
             new PurchaseInstallmentSnapshot(3, 5000m, 0, 0)]);
        var line = new ConfirmedSaleItemSnapshot("offer-1", null, "Original name",
            "package", 60, 3, 1, "installment", 20000m, 0m, 20000m,
            5000m, snapshot);
        db.Customers.AddRange(
            new Customer { Id = "customer-1", TenantId = "tenant-1", FullName = "A" },
            new Customer { Id = "customer-2", TenantId = "tenant-1", FullName = "B" });
        db.Sales.AddRange(
            Sale("sale-1", "customer-1", "branch-1", line),
            Sale("sale-2", "customer-1", "branch-2", line),
            Sale("sale-3", "customer-2", "branch-1", line));
        db.CustomerPackages.AddRange(
            Purchase("purchase-1", "customer-1", "sale-1", snapshot),
            Purchase("purchase-2", "customer-1", "sale-2", snapshot),
            Purchase("purchase-3", "customer-2", "sale-3", snapshot));
        db.CustomerPackageInstallments.AddRange(
            new CustomerPackageInstallment
            {
                TenantId = "tenant-1",
                CustomerPackageId = "purchase-1",
                Number = 1,
                Amount = 5000m,
                DueOn = new DateOnly(2026, 11, 29)
            },
            new CustomerPackageInstallment
            {
                TenantId = "tenant-1",
                CustomerPackageId = "purchase-1",
                Number = 2,
                Amount = 5000m,
                DueOn = new DateOnly(2026, 12, 29)
            },
            new CustomerPackageInstallment
            {
                TenantId = "tenant-1",
                CustomerPackageId = "purchase-1",
                Number = 3,
                Amount = 5000m,
                DueOn = new DateOnly(2027, 1, 29)
            });
        db.SaleCollections.Add(new SaleCollection
        {
            TenantId = "tenant-1",
            SaleId = "sale-1",
            Confirmed = true,
            PaymentMethod = "cash",
            CollectedByUserId = "operator-1",
            AttemptedAtUtc = clock.Object.UtcNow,
            ConfirmedAtUtc = clock.Object.UtcNow.AddDays(1)
        });
        db.InstallmentCollections.AddRange(
            new InstallmentCollection
            {
                TenantId = "tenant-1",
                CustomerPackageId = "purchase-1",
                InstallmentNumber = 1,
                Amount = 5000m,
                PaymentMethod = "card",
                CollectedByUserId = "operator-1",
                ConfirmedAtUtc = clock.Object.UtcNow
            },
            new InstallmentCollection
            {
                TenantId = "tenant-1",
                CustomerPackageId = "purchase-2",
                InstallmentNumber = 2,
                Amount = 5000m,
                PaymentMethod = "cash",
                CollectedByUserId = "operator-1",
                ConfirmedAtUtc = clock.Object.UtcNow
            });
        await db.SaveChangesAsync();

        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<GetCustomerPurchaseDetailsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetCustomerPurchaseDetailsQuery _, CancellationToken _) =>
                new CustomerPurchaseDetailsDto("purchase-1", clock.Object.UtcNow,
                    null, null, null, "active", "partial", null,
                    new PurchaseBalanceDto(2500, 1000, 1500), 20000m, 5000m,
                    15000m, 1, 3, [], [], true, 5000m, [], []));
        var handler = new GetCustomerStatementHandler(db, user.Object, scope.Object, mediator.Object);
        var result = await handler.Handle(
            new GetCustomerStatementQuery("customer-1", "purchase-1"), CancellationToken.None);

        result.ServiceName.Should().Be("Original name");
        result.OperationCount.Should().Be(1);
        result.Installments.Select(x => x.Status).Should().Equal("paid", "due", "due");
        result.Installments[0].PaymentMethod.Should().Be("card");
        result.Installments[1].PaidAtUtc.Should().BeNull();
        result.Installments[1].DueOn.Should().Be(new DateOnly(2026, 12, 29));
        result.FirstPayment!.PaidAtUtc.Should().Be(clock.Object.UtcNow.AddDays(1));
        result.Purchase.AmountPaid.Should().Be(10000m);
        result.SessionBalance.Should().Be(new PurchaseBalanceDto(3, 0, 3));
        mediator.Verify(x => x.Send(It.Is<GetCustomerPurchaseDetailsQuery>(q =>
            q.PurchaseId == "purchase-1" && q.AllowedBranchIds!.Contains("branch-1")),
            It.IsAny<CancellationToken>()), Times.Once);

        var legacyCollection = await db.SaleCollections.SingleAsync();
        legacyCollection.ConfirmedAtUtc = null;
        await db.SaveChangesAsync();
        var legacy = await handler.Handle(
            new GetCustomerStatementQuery("customer-1", "purchase-1"), CancellationToken.None);
        legacy.FirstPayment!.PaidAtUtc.Should().Be(clock.Object.UtcNow);

        var denied = () => handler.Handle(
            new GetCustomerStatementQuery("customer-1", "purchase-2"), CancellationToken.None);
        await denied.Should().ThrowAsync<ApiNotFoundException>();
        var wrongCustomer = () => handler.Handle(
            new GetCustomerStatementQuery("customer-2", "purchase-1"), CancellationToken.None);
        await wrongCustomer.Should().ThrowAsync<ApiNotFoundException>();
    }

    private static Sale Sale(string id, string customerId, string branchId,
        ConfirmedSaleItemSnapshot line) => new()
        {
            Id = id,
            TenantId = "tenant-1",
            CustomerId = customerId,
            BranchId = branchId,
            ItemsSnapshotJson = JsonSerializer.Serialize(new[] { line })
        };

    private static CustomerPackage Purchase(string id, string customerId,
        string saleId, PackagePurchaseSnapshot snapshot) => new()
        {
            Id = id,
            TenantId = "tenant-1",
            CustomerId = customerId,
            SaleId = saleId,
            SaleItemIndex = 0,
            PackageId = "offer-1",
            Total = 2500,
            PurchasePrice = 20000m,
            AmountDue = 15000m,
            PurchaseSnapshotJson = snapshot.ToJson()
        };
}
