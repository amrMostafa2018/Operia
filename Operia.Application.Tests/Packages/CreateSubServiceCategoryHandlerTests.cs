using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.Commands.CreateSubServiceCategory;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Domain.Exceptions;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Packages;

public sealed class CreateSubServiceCategoryHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly TestApplicationDbContext _db;
    private readonly IAuditWriter _auditWriter;
    private string? _tenantId = "tenant-1";

    public CreateSubServiceCategoryHandlerTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("CreateSubServiceCategoryTests_" + Guid.NewGuid())
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
    public async Task CreateSubServiceCategory_WithValidParent_SavesAndReturnsDto()
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"));
        await _db.SaveChangesAsync();

        var handler = new CreateSubServiceCategoryHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var result = await handler.Handle(new CreateSubServiceCategoryCommand("Upper Body", "cat-1"), default);

        result.Name.Should().Be("Upper Body");
        result.ServiceCategoryId.Should().Be("cat-1");
        (await _db.SubServiceCategories.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateSubServiceCategory_ParentNotFoundInTenant_ThrowsNotFoundException()
    {
        var handler = new CreateSubServiceCategoryHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var act = () => handler.Handle(new CreateSubServiceCategoryCommand("Upper Body", "missing-cat"), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateSubServiceCategory_ParentBelongsToDifferentTenant_ThrowsNotFoundException()
    {
        _tenantId = null;
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-2", "tenant-2", "Body Laser"));
        await _db.SaveChangesAsync();
        _tenantId = "tenant-1";

        var handler = new CreateSubServiceCategoryHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var act = () => handler.Handle(new CreateSubServiceCategoryCommand("Upper Body", "cat-2"), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateSubServiceCategory_DuplicateNameSameParent_ThrowsConflict_WithCode_SUB_CATEGORY_NAME_TAKEN()
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"));
        _db.SubServiceCategories.Add(PackageTestData.SubServiceCategory("sub-1", "tenant-1", "cat-1", "Upper Body"));
        await _db.SaveChangesAsync();

        var handler = new CreateSubServiceCategoryHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var act = () => handler.Handle(new CreateSubServiceCategoryCommand("upper body", "cat-1"), default);

        var exception = await act.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(ApiErrorCodes.Packages.SubCategoryNameTaken);
    }

    [Fact]
    public async Task CreateSubServiceCategory_DuplicateNameDifferentParent_Succeeds()
    {
        _db.ServiceCategories.AddRange(
            PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"),
            PackageTestData.ServiceCategory("cat-2", "tenant-1", "Face Laser"));
        _db.SubServiceCategories.Add(PackageTestData.SubServiceCategory("sub-1", "tenant-1", "cat-1", "Upper Body"));
        await _db.SaveChangesAsync();

        var handler = new CreateSubServiceCategoryHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var result = await handler.Handle(new CreateSubServiceCategoryCommand("Upper Body", "cat-2"), default);

        result.Name.Should().Be("Upper Body");
        result.ServiceCategoryId.Should().Be("cat-2");
    }

    [Fact]
    public async Task CreateSubServiceCategory_WritesAuditLog()
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"));
        await _db.SaveChangesAsync();

        var handler = new CreateSubServiceCategoryHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        await handler.Handle(new CreateSubServiceCategoryCommand("Lower Body", "cat-1"), default);

        var audit = await _db.AuditLogs.SingleAsync(x => x.Action == AuditActions.SubServiceCategoryCreated);
        audit.Should().NotBeNull();
    }
}
