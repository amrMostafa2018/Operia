using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.Commands.DeletePackage;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Enums;
using Operia.Domain.Exceptions;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Packages;

public sealed class DeletePackageHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly TestApplicationDbContext _db;
    private readonly IAuditWriter _auditWriter;
    private string? _tenantId = "tenant-1";

    public DeletePackageHandlerTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("DeletePackageTests_" + Guid.NewGuid())
            .Options;
        _db = new TestApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _auditWriter = new AuditWriter(_db, _currentUserServiceMock.Object);
        _currentUserServiceMock.Setup(x => x.TenantId).Returns(() => _tenantId);
        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-1");
    }

    [Fact]
    public async Task DeletePackage_SetsStatusToCancelled()
    {
        await SeedPackageAsync("pkg-1", "tenant-1", PackageStatus.Active);

        var handler = new DeletePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        await handler.Handle(new DeletePackageCommand("pkg-1"), default);

        var package = await _db.Packages.SingleAsync();
        package.Status.Should().Be(PackageStatus.Cancelled);
        (await _db.Packages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DeletePackage_AlreadyCancelled_IsIdempotent()
    {
        await SeedPackageAsync("pkg-1", "tenant-1", PackageStatus.Cancelled);

        var handler = new DeletePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        await handler.Handle(new DeletePackageCommand("pkg-1"), default);

        var package = await _db.Packages.SingleAsync();
        package.Status.Should().Be(PackageStatus.Cancelled);
        (await _db.AuditLogs.CountAsync(x => x.Action == AuditActions.PackageDeleted)).Should().Be(0);
    }

    [Fact]
    public async Task DeletePackage_PackageNotFound_ThrowsNotFoundException()
    {
        var handler = new DeletePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var act = () => handler.Handle(new DeletePackageCommand("missing-pkg"), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeletePackage_CrossTenantPackage_ThrowsNotFoundException()
    {
        _tenantId = null;
        await SeedPackageAsync("pkg-2", "tenant-2", PackageStatus.Active);
        _tenantId = "tenant-1";

        var handler = new DeletePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var act = () => handler.Handle(new DeletePackageCommand("pkg-2"), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeletePackage_WritesAuditLog_WithActionPackageDeleted()
    {
        await SeedPackageAsync("pkg-1", "tenant-1", PackageStatus.Active);

        var handler = new DeletePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        await handler.Handle(new DeletePackageCommand("pkg-1"), default);

        var audit = await _db.AuditLogs.SingleAsync(x => x.Action == AuditActions.PackageDeleted);
        audit.Should().NotBeNull();
    }

    private async Task SeedPackageAsync(string packageId, string tenantId, PackageStatus status)
    {
        var categoryId = $"cat-{tenantId}";
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory(categoryId, tenantId, "Body Laser"));
        _db.Packages.Add(PackageTestData.Package(packageId, tenantId, categoryId, "Offer", status: status));
        await _db.SaveChangesAsync();
    }
}
