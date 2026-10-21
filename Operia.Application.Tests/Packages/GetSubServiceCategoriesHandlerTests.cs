using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.Queries.GetSubServiceCategories;
using Operia.Application.Tests.Helpers;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Packages;

public sealed class GetSubServiceCategoriesHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly TestApplicationDbContext _db;
    private string? _tenantId = "tenant-1";

    public GetSubServiceCategoriesHandlerTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("GetSubServiceCategoriesTests_" + Guid.NewGuid())
            .Options;
        _db = new TestApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _currentUserServiceMock.Setup(x => x.TenantId).Returns(() => _tenantId);
        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-1");
    }

    [Fact]
    public async Task GetSubServiceCategories_WithParentFilter_ReturnsOnlyChildrenOfParent()
    {
        _db.ServiceCategories.AddRange(
            PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"),
            PackageTestData.ServiceCategory("cat-2", "tenant-1", "Face Laser"));
        _db.SubServiceCategories.AddRange(
            PackageTestData.SubServiceCategory("sub-1", "tenant-1", "cat-1", "Upper Body"),
            PackageTestData.SubServiceCategory("sub-2", "tenant-1", "cat-1", "Lower Body"),
            PackageTestData.SubServiceCategory("sub-3", "tenant-1", "cat-2", "Cheeks"));
        await _db.SaveChangesAsync();

        var handler = new GetSubServiceCategoriesHandler(_db, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetSubServiceCategoriesQuery("cat-1"), default);

        result.Should().HaveCount(2);
        result.Select(category => category.Id).Should().BeEquivalentTo("sub-1", "sub-2");
    }

    [Fact]
    public async Task GetSubServiceCategories_WithoutFilter_ReturnsAllSubCategoriesForTenant()
    {
        _db.ServiceCategories.AddRange(
            PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"),
            PackageTestData.ServiceCategory("cat-2", "tenant-1", "Face Laser"));
        _db.SubServiceCategories.AddRange(
            PackageTestData.SubServiceCategory("sub-1", "tenant-1", "cat-1", "Upper Body"),
            PackageTestData.SubServiceCategory("sub-2", "tenant-1", "cat-2", "Cheeks"));
        await _db.SaveChangesAsync();

        var handler = new GetSubServiceCategoriesHandler(_db, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetSubServiceCategoriesQuery(), default);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSubServiceCategories_CrossTenantParentId_ReturnsEmpty()
    {
        _tenantId = null;
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-2", "tenant-2", "Other Laser"));
        _db.SubServiceCategories.Add(PackageTestData.SubServiceCategory("sub-2", "tenant-2", "cat-2", "Other Sub"));
        await _db.SaveChangesAsync();
        _tenantId = "tenant-1";

        var handler = new GetSubServiceCategoriesHandler(_db, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetSubServiceCategoriesQuery("cat-2"), default);

        result.Should().BeEmpty();
    }
}
