using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.Queries.GetPackageById;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Enums;
using Operia.Domain.Exceptions;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Packages;

public sealed class GetPackageByIdHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly TestApplicationDbContext _db;
    private string? _tenantId = "tenant-1";

    public GetPackageByIdHandlerTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("GetPackageByIdTests_" + Guid.NewGuid())
            .Options;
        _db = new TestApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _currentUserServiceMock.Setup(x => x.TenantId).Returns(() => _tenantId);
        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-1");
    }

    [Fact]
    public async Task GetPackageById_ReturnsFullDetail()
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"));
        _db.Packages.Add(PackageTestData.Package(
            "pkg-1",
            "tenant-1",
            "cat-1",
            "Summer Offer",
            OfferType.Package,
            PackageStatus.Active,
            "sub-1"));
        await _db.SaveChangesAsync();

        var handler = new GetPackageByIdHandler(_db, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetPackageByIdQuery("pkg-1"), default);

        result.Id.Should().Be("pkg-1");
        result.Name.Should().Be("Summer Offer");
        result.OfferType.Should().Be("package");
        result.ServiceCategoryId.Should().Be("cat-1");
        result.ServiceCategoryName.Should().Be("Body Laser");
        result.SubServiceCategoryId.Should().Be("sub-1");
        result.Description.Should().Be("Description");
    }

    [Fact]
    public async Task GetPackageById_NotFound_ThrowsNotFoundException()
    {
        var handler = new GetPackageByIdHandler(_db, _currentUserServiceMock.Object);
        var act = () => handler.Handle(new GetPackageByIdQuery("missing-pkg"), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetPackageById_CrossTenantId_ThrowsNotFoundException()
    {
        _tenantId = null;
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-2", "tenant-2", "Body Laser"));
        _db.Packages.Add(PackageTestData.Package("pkg-2", "tenant-2", "cat-2", "Other Tenant Offer"));
        await _db.SaveChangesAsync();
        _tenantId = "tenant-1";

        var handler = new GetPackageByIdHandler(_db, _currentUserServiceMock.Object);
        var act = () => handler.Handle(new GetPackageByIdQuery("pkg-2"), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
