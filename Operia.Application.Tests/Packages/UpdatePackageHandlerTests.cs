using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.Commands.UpdatePackage;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Enums;
using Operia.Domain.Exceptions;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Packages;

public sealed class UpdatePackageHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly TestApplicationDbContext _db;
    private readonly IAuditWriter _auditWriter;
    private string? _tenantId = "tenant-1";

    public UpdatePackageHandlerTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("UpdatePackageTests_" + Guid.NewGuid())
            .Options;
        _db = new TestApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _auditWriter = new AuditWriter(_db, _currentUserServiceMock.Object);
        _currentUserServiceMock.Setup(x => x.TenantId).Returns(() => _tenantId);
        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-1");
    }

    [Fact]
    public async Task UpdatePackage_ChangesAllMutableFields()
    {
        await SeedPackageAsync("pkg-1", "cat-1", PackageStatus.Active);
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-2", "tenant-1", "Face Laser"));
        await _db.SaveChangesAsync();

        var handler = new UpdatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var command = new UpdatePackageCommand(
            "pkg-1",
            "Updated Offer",
            false,
            "package",
            "Updated description",
            "cat-2",
            null,
            60,
            10,
            200,
            18,
            2500m,
            "SAVE20",
            20m);

        var result = await handler.Handle(command, default);

        result.Name.Should().Be("Updated Offer");
        result.Description.Should().Be("Updated description");
        result.OfferType.Should().Be("package");
        result.ServiceCategoryId.Should().Be("cat-2");
        result.SessionDurationMinutes.Should().Be(60);
        result.SessionCount.Should().Be(10);
        result.PulseCount.Should().Be(200);
        result.PackageExpiryMonths.Should().Be(18);
        result.Price.Should().Be(2500m);
        result.DiscountCode.Should().Be("SAVE20");
        result.DiscountPercent.Should().Be(20m);
        result.Status.Should().Be("cancelled");
    }

    [Fact]
    public async Task UpdatePackage_PackageNotFound_ThrowsNotFoundException()
    {
        var handler = new UpdatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var act = () => handler.Handle(ValidUpdateCommand("missing-pkg", "cat-1"), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdatePackage_PackageAlreadyCancelled_ThrowsConflictException()
    {
        await SeedPackageAsync("pkg-1", "cat-1", PackageStatus.Cancelled);

        var handler = new UpdatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var act = () => handler.Handle(ValidUpdateCommand("pkg-1", "cat-1"), default);

        var exception = await act.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(ApiErrorCodes.Packages.PackageAlreadyCancelled);
    }

    [Fact]
    public async Task UpdatePackage_CrossTenantPackage_ThrowsNotFoundException()
    {
        _tenantId = null;
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-2", "Body Laser"));
        _db.Packages.Add(PackageTestData.Package("pkg-2", "tenant-2", "cat-1", "Other Tenant Offer"));
        await _db.SaveChangesAsync();
        _tenantId = "tenant-1";

        var handler = new UpdatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var act = () => handler.Handle(ValidUpdateCommand("pkg-2", "cat-1"), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdatePackage_WritesAuditLog_WithActionPackageUpdated()
    {
        await SeedPackageAsync("pkg-1", "cat-1", PackageStatus.Active);

        var handler = new UpdatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        await handler.Handle(ValidUpdateCommand("pkg-1", "cat-1"), default);

        var audit = await _db.AuditLogs.SingleAsync(x => x.Action == AuditActions.PackageUpdated);
        audit.Should().NotBeNull();
        audit.DetailsJson.Should().NotBeNullOrWhiteSpace();
        audit.DetailsJson.Should().Contain("\"previous\"");
        audit.DetailsJson.Should().Contain("\"current\"");
    }

    private async Task SeedPackageAsync(string packageId, string categoryId, PackageStatus status)
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory(categoryId, "tenant-1", "Body Laser"));
        _db.Packages.Add(PackageTestData.Package(packageId, "tenant-1", categoryId, "Original Offer", status: status));
        await _db.SaveChangesAsync();
    }

    private static UpdatePackageCommand ValidUpdateCommand(string packageId, string categoryId) =>
        new(
            packageId,
            "Updated Offer",
            true,
            "singleSession",
            "Updated description",
            categoryId,
            null,
            45,
            0,
            null,
            null,
            1500m,
            string.Empty,
            null);
}
