using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.Queries.GetPackages;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Packages;

public sealed class GetPackagesHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly TestApplicationDbContext _db;
    private string? _tenantId = "tenant-1";

    public GetPackagesHandlerTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("GetPackagesTests_" + Guid.NewGuid())
            .Options;
        _db = new TestApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _currentUserServiceMock.Setup(x => x.TenantId).Returns(() => _tenantId);
        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-1");
    }

    [Fact]
    public async Task GetPackages_ReturnsTenantScopedPage()
    {
        await SeedPackagesAsync();

        var handler = new GetPackagesHandler(_db, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetPackagesQuery(), default);

        result.Items.Should().HaveCount(4);
        result.Items.Select(item => item.Name).Should().BeEquivalentTo(
            "Alpha Offer",
            "Beta Offer",
            "Gamma Offer",
            "Delta Offer");
    }

    [Fact]
    public async Task GetPackages_FilterByStatus_Active_ReturnsOnlyActive()
    {
        await SeedPackagesAsync();

        var handler = new GetPackagesHandler(_db, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetPackagesQuery(Status: "active"), default);

        result.Items.Should().HaveCount(3);
        result.Items.Should().OnlyContain(item => item.Status == "active");
    }

    [Fact]
    public async Task GetPackages_FilterByStatus_Cancelled_ReturnsOnlyCancelled()
    {
        await SeedPackagesAsync();

        var handler = new GetPackagesHandler(_db, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetPackagesQuery(Status: "cancelled"), default);

        result.Items.Should().HaveCount(1);
        result.Items.Single().Name.Should().Be("Delta Offer");
    }

    [Fact]
    public async Task GetPackages_FilterByOfferType_Package_ReturnsPackageTypeOnly()
    {
        await SeedPackagesAsync();

        var handler = new GetPackagesHandler(_db, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetPackagesQuery(OfferType: "package"), default);

        result.Items.Should().HaveCount(1);
        result.Items.Single().Name.Should().Be("Beta Offer");
        result.Items.Single().OfferType.Should().Be("package");
    }

    [Fact]
    public async Task GetPackages_FilterByOfferType_SingleSession_ReturnsSingleSessionOnly()
    {
        await SeedPackagesAsync();

        var handler = new GetPackagesHandler(_db, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetPackagesQuery(OfferType: "singleSession"), default);

        result.Items.Should().HaveCount(3);
        result.Items.Should().OnlyContain(item => item.OfferType == "singleSession");
    }

    [Fact]
    public async Task GetPackages_FilterByServiceCategoryId_ReturnsCorrectSubset()
    {
        await SeedPackagesAsync();

        var handler = new GetPackagesHandler(_db, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetPackagesQuery(ServiceCategoryId: "cat-face"), default);

        result.Items.Should().HaveCount(1);
        result.Items.Single().Name.Should().Be("Gamma Offer");
    }

    [Fact]
    public async Task GetPackages_SearchByName_CaseInsensitive_ReturnsMatchingRows()
    {
        await SeedPackagesAsync();

        var handler = new GetPackagesHandler(_db, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetPackagesQuery(Search: "Alpha"), default);

        result.Items.Should().HaveCount(1);
        result.Items.Single().Name.Should().Be("Alpha Offer");
    }

    [Fact]
    public async Task GetPackages_ActiveAndCancelledCounts_ReflectActualTotals()
    {
        await SeedPackagesAsync();

        var handler = new GetPackagesHandler(_db, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetPackagesQuery(Status: "active"), default);

        result.ActiveCount.Should().Be(3);
        result.CancelledCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPackages_Pagination_SecondPageReturnsCorrectOffset()
    {
        var now = DateTime.UtcNow;
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-body", "tenant-1", "Body Laser"));
        await _db.SaveChangesAsync();

        await AddPackageWithCreatedAtAsync("pkg-1", "cat-body", "First Offer", now.AddMinutes(-3));
        await AddPackageWithCreatedAtAsync("pkg-2", "cat-body", "Second Offer", now.AddMinutes(-2));
        await AddPackageWithCreatedAtAsync("pkg-3", "cat-body", "Third Offer", now.AddMinutes(-1));

        var handler = new GetPackagesHandler(_db, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetPackagesQuery(PageNumber: 2, PageSize: 2), default);

        result.Items.Should().HaveCount(1);
        result.Items.Single().Name.Should().Be("First Offer");
        result.PageNumber.Should().Be(2);
        result.TotalCount.Should().Be(3);
        result.TotalPages.Should().Be(2);
    }

    private async Task AddPackageWithCreatedAtAsync(
        string id,
        string categoryId,
        string name,
        DateTime createdAt,
        OfferType offerType = OfferType.SingleSession,
        PackageStatus status = PackageStatus.Active)
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(createdAt);
        _db.Packages.Add(PackageTestData.Package(id, "tenant-1", categoryId, name, offerType, status));
        await _db.SaveChangesAsync();
    }

    private async Task SeedPackagesAsync()
    {
        var now = DateTime.UtcNow;
        _db.ServiceCategories.AddRange(
            PackageTestData.ServiceCategory("cat-body", "tenant-1", "Body Laser"),
            PackageTestData.ServiceCategory("cat-face", "tenant-1", "Face Laser"));
        await _db.SaveChangesAsync();

        await AddPackageWithCreatedAtAsync("pkg-a", "cat-body", "Alpha Offer", now.AddMinutes(-4));
        await AddPackageWithCreatedAtAsync("pkg-b", "cat-body", "Beta Offer", now.AddMinutes(-3), OfferType.Package);
        await AddPackageWithCreatedAtAsync("pkg-c", "cat-face", "Gamma Offer", now.AddMinutes(-2));
        await AddPackageWithCreatedAtAsync(
            "pkg-d",
            "cat-body",
            "Delta Offer",
            now.AddMinutes(-1),
            status: PackageStatus.Cancelled);

        _tenantId = null;
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-other", "tenant-2", "Other Laser"));
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(now);
        _db.Packages.Add(PackageTestData.Package("pkg-other", "tenant-2", "cat-other", "Other Tenant Offer"));
        await _db.SaveChangesAsync();
        _tenantId = "tenant-1";
    }
}
