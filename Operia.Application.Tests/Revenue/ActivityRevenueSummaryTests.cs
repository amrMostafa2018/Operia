using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Dashboard.Queries.GetDashboardRevenue;
using Operia.Application.Revenue.Queries.GetActivityRevenueSummary;
using Operia.Application.Revenue.Queries.GetActivityRevenueReport;
using Operia.Application.Revenue.Commands.SubmitRevenuePayment;
using Operia.Application.Revenue.Commands.ReviewRevenuePayment;
using Operia.Application.Revenue.Queries.ExportActivityRevenue;
using Operia.Application.Revenue.Queries.GetRevenuePaymentHistory;
using Operia.Application.Revenue.Queries.GetRevenuePaymentProof;
using Operia.Application.Common.Auditing;
using MediatR;
using System.IO.Compression;
using System.Xml.Linq;
using System.Text.Json;
using Operia.Application.Sales.Commands.ConfirmSale;
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
    public async Task ReviewConfirmationUsesReceiptDateRatherThanSubmissionDate()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.Sales.Add(new Sale
        {
            Id = "later-sale",
            TenantId = "tenant-1",
            BranchId = "branch-1",
            CustomerId = "customer-1",
            DueNow = 1200m,
            PaymentMethod = "bank_transfer",
            Status = "PendingCollection"
        });
        await fixture.Db.SaveChangesAsync();
        fixture.Clock.SetupGet(x => x.UtcNow).Returns(new DateTime(
            2026, 10, 27, 21, 0, 0, DateTimeKind.Utc));
        var store = new SaleCollectionStore(fixture.Db);
        var audit = new AuditWriter(fixture.Db, fixture.User.Object);
        var pending = await new SubmitRevenuePaymentHandler(fixture.Db, store,
            fixture.User.Object, fixture.Scope.Object, audit, fixture.Clock.Object)
            .Handle(new SubmitRevenuePaymentCommand("later-sale", "later-key", null, null),
                CancellationToken.None);
        fixture.Clock.SetupGet(x => x.UtcNow).Returns(new DateTime(
            2026, 10, 28, 22, 0, 0, DateTimeKind.Utc));
        await new ReviewRevenuePaymentHandler(fixture.Db, store,
            fixture.User.Object, fixture.Scope.Object, audit, fixture.Clock.Object)
            .Handle(new ReviewRevenuePaymentCommand(pending.PaymentId, true),
                CancellationToken.None);
        var summary = await fixture.Summary();
        summary.Today.GrossCollected.Should().Be(1200m);
        var report = await new GetActivityRevenueReportHandler(fixture.Db,
            fixture.User.Object, fixture.Scope.Object, fixture.Clock.Object)
            .Handle(new GetActivityRevenueReportQuery(
                new DateOnly(2026, 10, 29), new DateOnly(2026, 10, 29)),
                CancellationToken.None);
        report.Items.Should().ContainSingle(x => x.PaymentId == pending.PaymentId &&
            x.Status == "Paid");
    }

    [Fact]
    public async Task TransferProofIsReadableOnlyWithinAllowedSaleBranch()
    {
        await using var fixture = await Fixture.CreateAsync();
        var payment = await fixture.Db.SaleCollections.SingleAsync(x =>
            x.SaleId == "sale-other-branch");
        payment.ProofContent = [0xff, 0xd8, 0xff];
        payment.ProofContentType = "image/jpeg";
        await fixture.Db.SaveChangesAsync();
        var handler = new GetRevenuePaymentProofHandler(fixture.Db,
            fixture.User.Object, fixture.Scope.Object);
        var forbidden = () => handler.Handle(new GetRevenuePaymentProofQuery(payment.Id),
            CancellationToken.None);
        await forbidden.Should().ThrowAsync<ForbiddenException>();
        fixture.Scope.Setup(x => x.GetAllowedBranchIdsAsync("operator",
            It.IsAny<CancellationToken>())).ReturnsAsync(["branch-2"]);
        var proof = await handler.Handle(new GetRevenuePaymentProofQuery(payment.Id),
            CancellationToken.None);
        proof.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task CancellingPendingReviewDoesNotCreateRefundOrCollectedRevenue()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.Sales.Add(new Sale
        {
            Id = "cancel-sale",
            TenantId = "tenant-1",
            BranchId = "branch-1",
            CustomerId = "customer-1",
            DueNow = 1200m,
            PaymentMethod = "bank_transfer",
            Status = "PendingCollection"
        });
        await fixture.Db.SaveChangesAsync();
        var store = new SaleCollectionStore(fixture.Db);
        var audit = new AuditWriter(fixture.Db, fixture.User.Object);
        var pending = await new SubmitRevenuePaymentHandler(fixture.Db, store,
            fixture.User.Object, fixture.Scope.Object, audit, fixture.Clock.Object)
            .Handle(new SubmitRevenuePaymentCommand("cancel-sale", "cancel-key", null, null),
                CancellationToken.None);
        var review = new ReviewRevenuePaymentHandler(fixture.Db, store,
            fixture.User.Object, fixture.Scope.Object, audit, fixture.Clock.Object);
        await review.Handle(new ReviewRevenuePaymentCommand(pending.PaymentId, false),
            CancellationToken.None);
        await review.Handle(new ReviewRevenuePaymentCommand(pending.PaymentId, false),
            CancellationToken.None);
        (await fixture.Db.SaleCollections.SingleAsync(x => x.Id == pending.PaymentId))
            .Confirmed.Should().BeFalse();
        (await fixture.Db.RefundTransactions.CountAsync()).Should().Be(1);
        (await fixture.Db.AuditLogs.CountAsync(x =>
            x.Action == AuditActions.RevenuePaymentCancelled)).Should().Be(1);
        var report = await new GetActivityRevenueReportHandler(fixture.Db,
            fixture.User.Object, fixture.Scope.Object, fixture.Clock.Object)
            .Handle(new GetActivityRevenueReportQuery(), CancellationToken.None);
        report.Items.Should().ContainSingle(x => x.PaymentId == pending.PaymentId &&
            x.Status == "Cancelled");
        report.Totals.GrossCollections.Should().Be(5000m);
    }

    [Fact]
    public async Task ReportUsesInclusiveLocalDaysAndSavedServiceSnapshot()
    {
        await using var fixture = await Fixture.CreateAsync();
        var sale = await fixture.Db.Sales.SingleAsync(x => x.Id == "sale-today");
        sale.ItemsSnapshotJson = JsonSerializer.Serialize(new[]
        {
            new ConfirmedSaleItemSnapshot("offer-1", null, "Historical offer",
                "SingleSession", 30, 1, 1, "cash", 5000m, 0m, 5000m, 5000m, null)
        });
        await fixture.Db.SaveChangesAsync();
        var handler = new GetActivityRevenueReportHandler(fixture.Db,
            fixture.User.Object, fixture.Scope.Object, fixture.Clock.Object);
        var report = await handler.Handle(new GetActivityRevenueReportQuery(
            new DateOnly(2026, 10, 28), new DateOnly(2026, 10, 28),
            ServiceId: "offer-1"), CancellationToken.None);
        report.Total.Should().Be(3);
        report.Totals.GrossCollections.Should().Be(5000m);
        report.Totals.Refunds.Should().Be(1500m);
        report.Items.Should().Contain(x => x.Service == "Historical offer");
        var invalid = () => handler.Handle(new GetActivityRevenueReportQuery(
            new DateOnly(2026, 10, 29), new DateOnly(2026, 10, 28)),
            CancellationToken.None);
        await invalid.Should().ThrowAsync<ValidationException>();
        var unknownService = () => handler.Handle(new GetActivityRevenueReportQuery(
            ServiceId: "another-tenant-offer"), CancellationToken.None);
        await unknownService.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ExportIncludesAllFilteredRowsAndNumericTotals()
    {
        await using var fixture = await Fixture.CreateAsync();
        var report = await new GetActivityRevenueReportHandler(fixture.Db,
            fixture.User.Object, fixture.Scope.Object, fixture.Clock.Object)
            .Handle(new GetActivityRevenueReportQuery(AllRows: true), CancellationToken.None);
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<GetActivityRevenueReportQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        var workbook = await new ExportActivityRevenueHandler(mediator.Object)
            .Handle(new ExportActivityRevenueQuery(null, null, null, null),
                CancellationToken.None);
        using var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
        using var stream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(stream);
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var amountCells = xml.Descendants(spreadsheet + "c")
            .Where(x => ((string?)x.Attribute("r"))?.StartsWith("I") == true)
            .ToArray();
        amountCells.Should().HaveCount(report.Items.Count + 4);
        amountCells.Skip(1).Should().OnlyContain(x =>
            x.Attribute("t") == null && x.Element(spreadsheet + "v") != null);
        decimal.Parse(amountCells[^1].Element(spreadsheet + "v")!.Value,
            System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(report.Totals.NetCollected);
    }

    [Fact]
    public async Task PendingReviewConfirmsOnceAndAuditsWithTheCollection()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.Sales.Add(new Sale
        {
            Id = "review-sale",
            TenantId = "tenant-1",
            BranchId = "branch-1",
            CustomerId = "customer-1",
            DueNow = 1200m,
            PaymentMethod = "BankTransfer",
            Status = "PendingCollection"
        });
        await fixture.Db.SaveChangesAsync();
        var store = new SaleCollectionStore(fixture.Db);
        var audit = new AuditWriter(fixture.Db, fixture.User.Object);
        var submit = new SubmitRevenuePaymentHandler(fixture.Db, store,
            fixture.User.Object, fixture.Scope.Object, audit, fixture.Clock.Object);
        var review = new ReviewRevenuePaymentHandler(fixture.Db, store,
            fixture.User.Object, fixture.Scope.Object, audit, fixture.Clock.Object);

        var pending = await submit.Handle(new SubmitRevenuePaymentCommand(
            "review-sale", "review-key", null, null), CancellationToken.None);
        (await submit.Handle(new SubmitRevenuePaymentCommand(
            "review-sale", "review-key", null, null), CancellationToken.None))
            .PaymentId.Should().Be(pending.PaymentId);
        var report = new GetActivityRevenueReportHandler(fixture.Db,
            fixture.User.Object, fixture.Scope.Object, fixture.Clock.Object);
        (await report.Handle(new GetActivityRevenueReportQuery(), CancellationToken.None))
            .Items.Should().ContainSingle(x => x.PaymentId == pending.PaymentId &&
                x.Status == "UnderReview");
        await review.Handle(new ReviewRevenuePaymentCommand(pending.PaymentId, true),
            CancellationToken.None);
        await review.Handle(new ReviewRevenuePaymentCommand(pending.PaymentId, true),
            CancellationToken.None);
        (await fixture.Db.SaleCollections.CountAsync(x => x.SaleId == "review-sale"))
            .Should().Be(1);
        (await fixture.Db.AuditLogs.CountAsync(x =>
            x.Action == AuditActions.RevenuePaymentConfirmed)).Should().Be(1);
        var history = await new GetRevenuePaymentHistoryHandler(fixture.Db,
            fixture.User.Object, fixture.Scope.Object).Handle(
                new GetRevenuePaymentHistoryQuery(pending.PaymentId), CancellationToken.None);
        history.Select(x => x.NewStatus).Should().Equal("UnderReview", "Paid");
        (await report.Handle(new GetActivityRevenueReportQuery(), CancellationToken.None))
            .Totals.GrossCollections.Should().Be(6200m);
    }

    [Fact]
    public async Task ReportDefaultsToCenterMonthAndKeepsFailedAttemptOutOfMoney()
    {
        await using var fixture = await Fixture.CreateAsync();
        var handler = new GetActivityRevenueReportHandler(fixture.Db,
            fixture.User.Object, fixture.Scope.Object, fixture.Clock.Object);
        var report = await handler.Handle(new GetActivityRevenueReportQuery(),
            CancellationToken.None);
        report.FromDate.Should().Be(new DateOnly(2026, 10, 1));
        report.ToDate.Should().Be(new DateOnly(2026, 10, 28));
        report.Total.Should().Be(3);
        report.Totals.GrossCollections.Should().Be(5000m);
        report.Totals.Refunds.Should().Be(1500m);
        report.Totals.NetCollected.Should().Be(3500m);
        report.Items.Should().ContainSingle(x => x.Status == "Failed" && x.Amount == 900m);
        report.Today.TransactionCount.Should().Be(1);
        report.Today.CountChangePercent.Should().BeNull();
        report.Today.AmountChangePercent.Should().BeNull();
        report.Month.GrossCollections.Should().Be(5000m);
        report.Month.CountChangePercent.Should().Be(0m);
        report.Month.AmountChangePercent.Should().Be(0m);
    }

    [Fact]
    public async Task ReportPagesHaveStableUniqueMovementIdsAndRejectOtherBranch()
    {
        await using var fixture = await Fixture.CreateAsync();
        var handler = new GetActivityRevenueReportHandler(fixture.Db,
            fixture.User.Object, fixture.Scope.Object, fixture.Clock.Object);
        var first = await handler.Handle(new GetActivityRevenueReportQuery(PageSize: 1),
            CancellationToken.None);
        var second = await handler.Handle(new GetActivityRevenueReportQuery(Page: 2, PageSize: 1),
            CancellationToken.None);
        first.Total.Should().Be(second.Total);
        first.Items.Single().TransactionId.Should().NotBe(second.Items.Single().TransactionId);
        var forbidden = () => handler.Handle(new GetActivityRevenueReportQuery(
            BranchId: "branch-2"), CancellationToken.None);
        await forbidden.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task TableFiltersNarrowRowsWithoutChangingPeriodCards()
    {
        await using var fixture = await Fixture.CreateAsync();
        var collection = await fixture.Db.SaleCollections.SingleAsync(x =>
            x.SaleId == "sale-today" && x.Confirmed);
        collection.CollectedByUserId = "collector-1";
        fixture.Db.Employees.Add(new Employee
        {
            Id = "emp-1",
            TenantId = "tenant-1",
            IdentityUserId = "collector-1",
            FullName = "Nora",
            Code = "E1"
        });
        await fixture.Db.SaveChangesAsync();
        var handler = new GetActivityRevenueReportHandler(fixture.Db,
            fixture.User.Object, fixture.Scope.Object, fixture.Clock.Object);
        var all = await handler.Handle(new GetActivityRevenueReportQuery(), CancellationToken.None);
        var byEmployee = await handler.Handle(
            new GetActivityRevenueReportQuery(EmployeeId: "emp-1"), CancellationToken.None);
        byEmployee.Total.Should().Be(1);
        byEmployee.Items.Should().ContainSingle(x =>
            x.CollectedByUserId == "collector-1" && x.Status == "Paid");
        byEmployee.Today.Should().BeEquivalentTo(all.Today);
        byEmployee.Month.Should().BeEquivalentTo(all.Month);
        var missingEmployee = await handler.Handle(
            new GetActivityRevenueReportQuery(EmployeeId: "missing"), CancellationToken.None);
        missingEmployee.Total.Should().Be(0);
        missingEmployee.Today.Should().BeEquivalentTo(all.Today);
        var byName = await handler.Handle(
            new GetActivityRevenueReportQuery(CustomerName: "cust"), CancellationToken.None);
        byName.Total.Should().Be(all.Total);
        var none = await handler.Handle(
            new GetActivityRevenueReportQuery(Search: "no-such-invoice"), CancellationToken.None);
        none.Total.Should().Be(0);
        none.Today.TransactionCount.Should().Be(all.Today.TransactionCount);
    }

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
            fixture.User.Object, fixture.Scope.Object, fixture.Clock.Object).Handle(
                new GetDashboardRevenueQuery(),
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
    public async Task AttributedHistoricalInstallmentCountsWithoutASale()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "historical-purchase",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "offer-1",
            BranchId = "branch-1",
            BranchAttributionSource = "Reservation",
            Total = 1
        });
        fixture.Db.InstallmentCollections.Add(new InstallmentCollection
        {
            TenantId = "tenant-1",
            CustomerPackageId = "historical-purchase",
            InstallmentNumber = 1,
            Amount = 2000m,
            ConfirmedAtUtc = TodayFrom.AddHours(13)
        });
        await fixture.Db.SaveChangesAsync();

        (await fixture.Summary()).Today.GrossCollected.Should().Be(7000m);
        fixture.Scope.Setup(x => x.GetAllowedBranchIdsAsync("operator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-2"]);
        (await fixture.Summary()).Today.GrossCollected.Should().Be(7000m);
    }

    [Fact]
    public async Task AnotherTenantCannotReadCollectionsEvenWithBranchId()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.User.SetupGet(x => x.TenantId).Returns("tenant-2");
        var summary = () => fixture.Summary("branch-1");
        await summary.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task TodayUsesSavedBusinessTimezoneAcrossUtcMidnight()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 10, 27, 22, 30, 0,
            DateTimeKind.Utc));
        fixture.Db.SaleCollections.Add(new SaleCollection
        {
            TenantId = "tenant-1",
            SaleId = "sale-today",
            Amount = 1000m,
            Confirmed = true,
            AttemptedAtUtc = new DateTime(2026, 10, 27, 22, 15, 0,
                DateTimeKind.Utc)
        });
        await fixture.Db.SaveChangesAsync();
        var summary = await fixture.Summary();
        summary.Today.TransactionCount.Should().Be(2);
        summary.Today.GrossCollected.Should().Be(6000m);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public TestApplicationDbContext Db { get; }
        public Mock<ICurrentUserService> User { get; }
        public Mock<IBranchScope> Scope { get; }
        public Mock<IDateTimeProvider> Clock { get; }

        private Fixture(TestApplicationDbContext db, Mock<ICurrentUserService> user,
            Mock<IBranchScope> scope, Mock<IDateTimeProvider> clock)
        {
            Db = db;
            User = user;
            Scope = scope;
            Clock = clock;
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
            db.Tenants.Add(new Tenant { Id = "tenant-1", Timezone = "Africa/Cairo" });
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
                BranchId = "branch-1",
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
            return new Fixture(db, user, scope, clock);
        }

        public Task<ActivityRevenueSummaryDto> Summary(string? branchId = null) =>
            new GetActivityRevenueSummaryHandler(Db, User.Object, Scope.Object, Clock.Object)
                .Handle(new GetActivityRevenueSummaryQuery(branchId), CancellationToken.None);

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
