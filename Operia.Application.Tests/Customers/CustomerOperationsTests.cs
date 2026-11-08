using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Interfaces;
using Operia.Application.Customers.Queries.ExportCustomerOperations;
using Operia.Application.Customers.Queries.ListCustomerOperations;
using Operia.Application.Packages;
using Operia.Application.Sales.Commands.ConfirmSale;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Customers;

public sealed class CustomerOperationsTests
{
    [Fact]
    public async Task HistoricalPurchasesRequireEstablishedAllowedBranchAndKeepUnknownPricesBlank()
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
            .UseInMemoryDatabase($"HistoricalOperations_{Guid.NewGuid()}").Options;
        await using var db = new TestApplicationDbContext(options, clock.Object, user.Object);
        db.Tenants.Add(new Tenant { Id = "tenant-1", Timezone = "Africa/Cairo" });
        db.Branches.AddRange(
            new Branch { Id = "branch-1", TenantId = "tenant-1", Name = "Allowed" },
            new Branch { Id = "branch-2", TenantId = "tenant-1", Name = "Hidden" });
        db.Customers.Add(new Customer
        {
            Id = "customer-1",
            TenantId = "tenant-1",
            FullName = "Historical customer",
            MobileNumber = "01012345678"
        });
        db.CustomerPackages.AddRange(
            new CustomerPackage
            {
                Id = "known",
                TenantId = "tenant-1",
                CustomerId = "customer-1",
                PackageId = "offer-1",
                BranchId = "branch-1",
                PurchasePrice = 20000m,
                AmountDue = 15000m,
                Total = 10
            },
            new CustomerPackage
            {
                Id = "unknown-price",
                TenantId = "tenant-1",
                CustomerId = "customer-1",
                PackageId = "offer-1",
                BranchId = "branch-1",
                Total = 10
            },
            new CustomerPackage
            {
                Id = "unresolved",
                TenantId = "tenant-1",
                CustomerId = "customer-1",
                PackageId = "offer-1",
                PurchasePrice = 20000m,
                Total = 10
            },
            new CustomerPackage
            {
                Id = "other-branch",
                TenantId = "tenant-1",
                CustomerId = "customer-1",
                PackageId = "offer-1",
                BranchId = "branch-2",
                PurchasePrice = 20000m,
                Total = 10
            });
        await db.SaveChangesAsync();

        var page = await new ListCustomerOperationsHandler(new CustomerOperationsReader(
            db, user.Object, scope.Object, clock.Object)).Handle(
            new ListCustomerOperationsQuery(new CustomerOperationsFilter()), CancellationToken.None);

        page.Rows.Select(x => x.Id).Should().BeEquivalentTo(["known", "unknown-price"]);
        page.TotalSales.Should().Be(20000m);
        page.TotalCollected.Should().Be(5000m);
        page.Rows.Single(x => x.Id == "unknown-price").Total.Should().BeNull();
    }

    [Fact]
    public async Task ExportIncludesEveryMatchingRowBeyondTheGridPage()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc));
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns("tenant-1");
        user.SetupGet(x => x.UserId).Returns("operator-1");
        var branchScope = new Mock<IBranchScope>();
        branchScope.Setup(x => x.GetAllowedBranchIdsAsync("operator-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-1"]);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"CustomerOperations_{Guid.NewGuid()}").Options;
        await using var db = new TestApplicationDbContext(options, clock.Object, user.Object);
        db.Tenants.Add(new Tenant { Id = "tenant-1", Timezone = "Africa/Cairo" });
        db.Branches.Add(new Branch { Id = "branch-1", TenantId = "tenant-1", Name = "Allowed" });
        db.Customers.Add(new Customer
        {
            Id = "customer-1",
            TenantId = "tenant-1",
            FullName = "One customer",
            MobileNumber = "01012345678",
            NormalizedMobileNumber = "01012345678"
        });
        var line = new ConfirmedSaleItemSnapshot(
            null, null, "Service", "singleSession", 30, 1, 1, "cash",
            100m, 0m, 100m, 100m, null);
        var items = JsonSerializer.Serialize(new[] { line });
        for (var index = 0; index < 6; index++)
        {
            db.Sales.Add(new Sale
            {
                Id = $"sale-{index}",
                TenantId = "tenant-1",
                BranchId = "branch-1",
                CustomerId = "customer-1",
                ItemsSnapshotJson = items
            });
        }
        await db.SaveChangesAsync();

        var reader = new CustomerOperationsReader(db, user.Object, branchScope.Object, clock.Object);
        var filter = new CustomerOperationsFilter(Mobile: "01012345678");
        var page = await new ListCustomerOperationsHandler(reader)
            .Handle(new ListCustomerOperationsQuery(filter, PageSize: 5), CancellationToken.None);
        page.TotalCount.Should().Be(6);
        page.Rows.Should().HaveCount(5);

        var bytes = await new ExportCustomerOperationsHandler(reader)
            .Handle(new ExportCustomerOperationsQuery(filter), CancellationToken.None);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(sheet);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        xml.Descendants(ns + "row").Should().HaveCount(7);
    }

    [Fact]
    public async Task GridAndExportShareFilteredBalancesAndKeepStatusesSeparate()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc));
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns("tenant-1");
        user.SetupGet(x => x.UserId).Returns("operator-1");
        var branchScope = new Mock<IBranchScope>();
        branchScope.Setup(x => x.GetAllowedBranchIdsAsync("operator-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-1"]);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"CustomerOperations_{Guid.NewGuid()}").Options;
        await using var db = new TestApplicationDbContext(options, clock.Object, user.Object);
        db.Tenants.Add(new Tenant { Id = "tenant-1", Timezone = "Africa/Cairo" });
        db.Branches.AddRange(
            new Branch { Id = "branch-1", TenantId = "tenant-1", Name = "Allowed" },
            new Branch { Id = "branch-2", TenantId = "tenant-1", Name = "Hidden" });
        db.Customers.Add(new Customer
        {
            Id = "customer-1",
            TenantId = "tenant-1",
            FullName = "Blocked customer",
            MobileNumber = "01012345678",
            NormalizedMobileNumber = "01012345678",
            IsBlocked = true
        });
        var line = new ConfirmedSaleItemSnapshot(
            "offer-1", null, "Ten sessions", "package", 60, 10, 1, "installment",
            20000m, 0m, 20000m, 5000m, null);
        var items = JsonSerializer.Serialize(new[] { line });
        db.Sales.AddRange(
            new Sale
            {
                Id = "sale-1",
                TenantId = "tenant-1",
                BranchId = "branch-1",
                CustomerId = "customer-1",
                ItemsSnapshotJson = items,
                Status = "ReadyToBook"
            },
            new Sale
            {
                Id = "sale-2",
                TenantId = "tenant-1",
                BranchId = "branch-2",
                CustomerId = "customer-1",
                ItemsSnapshotJson = items,
                Status = "ReadyToBook"
            });
        var terms = new PackagePurchaseSnapshot("Ten sessions", OfferType.Package,
            60, 10, null, null, "category", null, "installment", 20000m, 20000m,
            null, 5000m,
            [
                new PurchaseInstallmentSnapshot(1, 7500m, 5, 5),
                new PurchaseInstallmentSnapshot(2, 7500m, 5, 10)
            ]);
        db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "purchase-1",
            TenantId = "tenant-1",
            SaleId = "sale-1",
            SaleItemIndex = 0,
            CustomerId = "customer-1",
            PackageId = "offer-1",
            PurchasePrice = 20000m,
            AmountDue = 15000m,
            PurchaseSnapshotJson = terms.ToJson(),
            Total = 10,
            Used = 2,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var reader = new CustomerOperationsReader(db, user.Object, branchScope.Object, clock.Object);
        var filter = new CustomerOperationsFilter(Mobile: "01012345678");
        var page = await new ListCustomerOperationsHandler(reader)
            .Handle(new ListCustomerOperationsQuery(filter), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.TotalSales.Should().Be(20000m);
        page.TotalCollected.Should().Be(5000m);
        page.TotalRemaining.Should().Be(15000m);
        page.Rows[0].CustomerRestriction.Should().Be("blocked");
        page.Rows[0].PurchaseStatus.Should().Be("active");
        page.Rows[0].PaymentStatus.Should().Be("partial");
        page.Rows[0].InstallmentDueStatus.Should().Be("due");

        var bytes = await new ExportCustomerOperationsHandler(reader)
            .Handle(new ExportCustomerOperationsQuery(filter), CancellationToken.None);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(sheet);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var cells = xml.Descendants(ns + "c")
            .ToDictionary(x => (string)x.Attribute("r")!, x => x);
        cells["C2"].Attribute("t")!.Value.Should().Be("inlineStr");
        cells["C2"].Descendants(ns + "t").Single().Value.Should().Be("01012345678");
        cells["S2"].Attribute("s")!.Value.Should().Be("1");
        cells["S2"].Element(ns + "v")!.Value.Should().NotBeNullOrWhiteSpace();
        cells["H2"].Element(ns + "v")!.Value.Should().Be("20000");
        cells["I2"].Element(ns + "v")!.Value.Should().Be("5000");
        cells["J2"].Element(ns + "v")!.Value.Should().Be("0");
        cells["K2"].Element(ns + "v")!.Value.Should().Be("5000");
        cells["L2"].Element(ns + "v")!.Value.Should().Be("15000");
        xml.Descendants(ns + "row").Should().HaveCount(2);

        var purchase = await db.CustomerPackages.SingleAsync();
        purchase.AmountDue = 7500m;
        await db.SaveChangesAsync();
        var later = await new ListCustomerOperationsHandler(reader)
            .Handle(new ListCustomerOperationsQuery(filter), CancellationToken.None);
        later.Rows[0].InstallmentDueStatus.Should().Be("scheduled");
    }

    [Fact]
    public async Task MobileAndDateFiltersUseTheSavedLocalDayInGridAndExport()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 11, 8, 9, 0, 0, DateTimeKind.Utc));
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns("tenant-1");
        user.SetupGet(x => x.UserId).Returns("operator-1");
        var branchScope = new Mock<IBranchScope>();
        branchScope.Setup(x => x.GetAllowedBranchIdsAsync("operator-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-1"]);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"LocalCustomerOperations_{Guid.NewGuid()}").Options;
        await using var db = new TestApplicationDbContext(options, clock.Object, user.Object);
        db.Tenants.Add(new Tenant { Id = "tenant-1", Timezone = "Africa/Cairo" });
        db.Branches.Add(new Branch { Id = "branch-1", TenantId = "tenant-1", Name = "Allowed" });
        db.Customers.Add(new Customer
        {
            Id = "customer-1",
            TenantId = "tenant-1",
            FullName = "Local customer",
            MobileNumber = "01012345678",
            NormalizedMobileNumber = "201012345678"
        });
        db.Sales.Add(new Sale
        {
            Id = "sale-1",
            TenantId = "tenant-1",
            BranchId = "branch-1",
            CustomerId = "customer-1",
            ItemsSnapshotJson = JsonSerializer.Serialize(new[]
            {
                new ConfirmedSaleItemSnapshot(null, null, "Service", "unlisted", 30, 1,
                    1, "cash", 100m, 0m, 100m, 100m, null)
            })
        });
        await db.SaveChangesAsync();
        var savedSale = await db.Sales.SingleAsync(x => x.Id == "sale-1");
        savedSale.CreatedAt = new DateTime(2026, 11, 7, 22, 30, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var reader = new CustomerOperationsReader(db, user.Object, branchScope.Object, clock.Object);
        var localDay = new DateOnly(2026, 11, 8);
        var filter = new CustomerOperationsFilter(localDay, localDay, Mobile: "+201012345678");
        var page = await new ListCustomerOperationsHandler(reader)
            .Handle(new ListCustomerOperationsQuery(filter), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.TimezoneId.Should().Be("Africa/Cairo");
        var previousDay = await new ListCustomerOperationsHandler(reader)
            .Handle(new ListCustomerOperationsQuery(new CustomerOperationsFilter(
                new DateOnly(2026, 11, 7), new DateOnly(2026, 11, 7),
                Mobile: "+201012345678")), CancellationToken.None);
        previousDay.TotalCount.Should().Be(0);

        var bytes = await new ExportCustomerOperationsHandler(reader)
            .Handle(new ExportCustomerOperationsQuery(filter), CancellationToken.None);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(sheet);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var dateCell = xml.Descendants(ns + "c")
            .Single(x => (string?)x.Attribute("r") == "S2");
        var localExportDate = DateTime.FromOADate(double.Parse(
            dateCell.Element(ns + "v")!.Value,
            System.Globalization.CultureInfo.InvariantCulture));
        localExportDate.Should().Be(new DateTime(2026, 11, 8, 0, 30, 0));
    }
}
