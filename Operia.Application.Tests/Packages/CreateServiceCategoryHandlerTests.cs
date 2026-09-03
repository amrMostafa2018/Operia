using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.Commands.CreateServiceCategory;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Packages;

public sealed class CreateServiceCategoryHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly TestApplicationDbContext _db;
    private readonly IAuditWriter _auditWriter;
    private string? _tenantId = "tenant-1";

    public CreateServiceCategoryHandlerTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("CreateServiceCategoryTests_" + Guid.NewGuid())
            .Options;
        _db = new TestApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _auditWriter = new AuditWriter(_db, _currentUserServiceMock.Object);
        _currentUserServiceMock.Setup(x => x.TenantId).Returns(() => _tenantId);
        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-1");
        _db.Businesses.Add(BranchTestData.Business("tenant-1"));
        _db.Businesses.Add(BranchTestData.Business("tenant-2"));
        _db.SaveChanges();
    }

    [Fact]
    public async Task CreateServiceCategory_WithValidData_SavesEntityAndReturnsDto()
    {
        var handler = new CreateServiceCategoryHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var result = await handler.Handle(new CreateServiceCategoryCommand("Body Laser", "pi-tag"), default);
        await _db.SaveChangesAsync();

        result.Name.Should().Be("Body Laser");
        result.Icon.Should().Be("pi-tag");
        (await _db.ServiceCategories.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateServiceCategory_DuplicateNameSameTenant_ThrowsConflict()
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"));
        await _db.SaveChangesAsync();

        var handler = new CreateServiceCategoryHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var act = () => handler.Handle(new CreateServiceCategoryCommand("body laser", "pi-tag"), default);

        var exception = await act.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(ApiErrorCodes.Packages.CategoryNameTaken);
    }

    [Fact]
    public async Task CreateServiceCategory_DuplicateNameDifferentTenant_Succeeds()
    {
        _tenantId = null;
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-2", "Body Laser"));
        await _db.SaveChangesAsync();
        _tenantId = "tenant-1";

        var handler = new CreateServiceCategoryHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var result = await handler.Handle(new CreateServiceCategoryCommand("Body Laser", "pi-tag"), default);
        await _db.SaveChangesAsync();

        result.Name.Should().Be("Body Laser");
    }

    [Fact]
    public async Task CreateServiceCategory_WritesAuditLog()
    {
        var handler = new CreateServiceCategoryHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        await handler.Handle(new CreateServiceCategoryCommand("Face Laser", "pi-tag"), default);
        await _db.SaveChangesAsync();

        var audit = await _db.AuditLogs.SingleAsync(x => x.Action == AuditActions.ServiceCategoryCreated);
        audit.Should().NotBeNull();
        audit.DetailsJson.Should().NotBeNullOrWhiteSpace();
        audit.DetailsJson.Should().Contain("\"name\":\"Body Laser\"");
    }
}
