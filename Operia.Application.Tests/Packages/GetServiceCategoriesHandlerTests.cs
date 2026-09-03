using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.Queries.GetServiceCategories;
using Operia.Application.Tests.Helpers;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Packages;

public sealed class GetServiceCategoriesHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly TestApplicationDbContext _db;
    private string? _tenantId = "tenant-1";

    public GetServiceCategoriesHandlerTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("GetServiceCategoriesTests_" + Guid.NewGuid())
            .Options;
        _db = new TestApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _currentUserServiceMock.Setup(x => x.TenantId).Returns(() => _tenantId);
        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-1");
    }

    [Fact]
    public async Task GetServiceCategories_ReturnsOnlyCallerTenantCategories()
    {
        _db.ServiceCategories.AddRange(
            PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"),
            PackageTestData.ServiceCategory("cat-2", "tenant-1", "Face Laser"));
        await _db.SaveChangesAsync();

        _tenantId = null;
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-3", "tenant-2", "Other Laser"));
        await _db.SaveChangesAsync();
        _tenantId = "tenant-1";

        var handler = new GetServiceCategoriesHandler(_db, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetServiceCategoriesQuery(), default);

        result.Should().HaveCount(2);
        result.Select(category => category.Id).Should().BeEquivalentTo("cat-1", "cat-2");
    }

    [Fact]
    public async Task GetServiceCategories_EmptyTenant_ReturnsEmptyList()
    {
        var handler = new GetServiceCategoriesHandler(_db, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetServiceCategoriesQuery(), default);

        result.Should().BeEmpty();
    }
}
