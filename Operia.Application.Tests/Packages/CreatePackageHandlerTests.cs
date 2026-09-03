using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.Commands.CreatePackage;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;
using ValidationException = Operia.Application.Common.Exceptions.ValidationException;

namespace Operia.Application.Tests.Packages;

public sealed class CreatePackageHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly TestApplicationDbContext _db;
    private readonly IAuditWriter _auditWriter;
    private string? _tenantId = "tenant-1";

    public CreatePackageHandlerTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("CreatePackageTests_" + Guid.NewGuid())
            .Options;
        _db = new TestApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _auditWriter = new AuditWriter(_db, _currentUserServiceMock.Object);
        _currentUserServiceMock.Setup(x => x.TenantId).Returns(() => _tenantId);
        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-1");
        _db.Businesses.Add(BranchTestData.Business("tenant-1"));
        _db.SaveChanges();
    }

    [Fact]
    public async Task CreatePackage_SingleSession_SavesCorrectly()
    {
        await SeedCategoryAsync("cat-1");
        var handler = new CreatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);

        var result = await handler.Handle(ValidCommand("cat-1", "singleSession"), default);

        result.OfferType.Should().Be("singleSession");
        result.SessionCount.Should().Be(0);
        result.PulseCount.Should().BeNull();
        result.PackageExpiryMonths.Should().BeNull();

        var saved = await _db.Packages.SingleAsync();
        saved.OfferType.Should().Be(OfferType.SingleSession);
        saved.SessionCount.Should().Be(0);
        saved.PulseCount.Should().BeNull();
        saved.PackageExpiryMonths.Should().BeNull();
    }

    [Fact]
    public async Task CreatePackage_PackageType_SavesSessionCountAndPulse()
    {
        await SeedCategoryAsync("cat-1");
        var handler = new CreatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var command = ValidCommand("cat-1", "package") with
        {
            SessionCount = 8,
            PulseCount = 120,
            PackageExpiryMonths = 6
        };

        var result = await handler.Handle(command, default);

        result.OfferType.Should().Be("package");
        result.SessionCount.Should().Be(8);
        result.PulseCount.Should().Be(120);
        result.PackageExpiryMonths.Should().Be(6);
    }

    [Fact]
    public async Task CreatePackage_UnknownServiceCategoryId_ThrowsValidationException()
    {
        var handler = new CreatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var act = () => handler.Handle(ValidCommand("missing-cat"), default);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes.Keys.Should().Contain(k => k.Equals("serviceCategoryId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreatePackage_WithDiscount_PersistsDiscountFields()
    {
        await SeedCategoryAsync("cat-1");
        var handler = new CreatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        var command = ValidCommand("cat-1") with
        {
            DiscountCode = "WELCOME15",
            DiscountPercent = 15m
        };

        var result = await handler.Handle(command, default);

        result.DiscountCode.Should().Be("WELCOME15");
        result.DiscountPercent.Should().Be(15m);
    }

    [Fact]
    public async Task CreatePackage_WritesAuditLog_WithActionPackageCreated()
    {
        await SeedCategoryAsync("cat-1");
        var handler = new CreatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        await handler.Handle(ValidCommand("cat-1"), default);

        var audit = await _db.AuditLogs.SingleAsync(x => x.Action == AuditActions.PackageCreated);
        audit.Should().NotBeNull();
        audit.DetailsJson.Should().NotBeNullOrWhiteSpace();
        audit.DetailsJson.Should().Contain("\"offerType\":\"singleSession\"");
    }

    private async Task SeedCategoryAsync(string categoryId)
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory(categoryId, "tenant-1", "Body Laser"));
        await _db.SaveChangesAsync();
    }

    private static CreatePackageCommand ValidCommand(
        string serviceCategoryId,
        string offerType = "singleSession",
        string? subServiceCategoryId = null) =>
        new(
            "Summer Offer",
            true,
            offerType,
            "Full body session",
            serviceCategoryId,
            subServiceCategoryId,
            45,
            offerType == "package" ? 6 : 0,
            offerType == "package" ? 100 : null,
            offerType == "package" ? 12 : null,
            1500m,
            string.Empty,
            null);
}
