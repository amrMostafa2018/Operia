using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.Commands.CreatePackage;
using Operia.Application.Tests.Helpers;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;
using Xunit;
using ValidationException = Operia.Application.Common.Exceptions.ValidationException;

namespace Operia.Application.Tests.Packages;

public sealed class CreatePackageValidatorTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly TestApplicationDbContext _db;
    private readonly IAuditWriter _auditWriter;
    private readonly CreatePackageValidator _validator = new();
    private string? _tenantId = "tenant-1";

    public CreatePackageValidatorTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("CreatePackageValidatorTests_" + Guid.NewGuid())
            .Options;
        _db = new TestApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _auditWriter = new AuditWriter(_db, _currentUserServiceMock.Object);
        _currentUserServiceMock.Setup(x => x.TenantId).Returns(() => _tenantId);
        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-1");
    }

    [Fact]
    public async Task CreatePackage_EmptyName_ThrowsValidationException_WithPackageNameRequired()
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"));
        await _db.SaveChangesAsync();

        var act = () => HandleAsync(ValidCommand() with { Name = string.Empty });

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes["name"].Should().Contain(ApiErrorCodes.Packages.PackageNameRequired);
    }

    [Fact]
    public async Task CreatePackage_NameTooLong_ThrowsValidationException_WithPackageNameMax()
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"));
        await _db.SaveChangesAsync();

        var act = () => HandleAsync(ValidCommand() with { Name = new string('A', 201) });

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes["name"].Should().Contain(ApiErrorCodes.Packages.PackageNameMax);
    }

    [Fact]
    public async Task CreatePackage_DescriptionTooLong_ThrowsValidationException_WithPackageDescriptionMax()
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"));
        await _db.SaveChangesAsync();

        var act = () => HandleAsync(ValidCommand() with { Description = new string('D', 251) });

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes["description"].Should().Contain(ApiErrorCodes.Packages.PackageDescriptionMax);
    }

    [Fact]
    public async Task CreatePackage_EmptyServiceCategoryId_ThrowsValidationException_WithPackageCategoryRequired()
    {
        var act = () => HandleAsync(ValidCommand() with { ServiceCategoryId = string.Empty });

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes["serviceCategoryId"].Should().Contain(ApiErrorCodes.Packages.PackageCategoryRequired);
    }

    [Fact]
    public async Task CreatePackage_InvalidSessionDuration_ThrowsValidationException_WithPackageDurationMin()
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"));
        await _db.SaveChangesAsync();

        var act = () => HandleAsync(ValidCommand() with { SessionDurationMinutes = 0 });

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes["sessionDurationMinutes"].Should().Contain(ApiErrorCodes.Packages.PackageDurationMin);
    }

    [Fact]
    public async Task CreatePackage_NegativePrice_ThrowsValidationException_WithPackagePriceMin()
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"));
        await _db.SaveChangesAsync();

        var act = () => HandleAsync(ValidCommand() with { Price = -1m });

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes["price"].Should().Contain(ApiErrorCodes.Packages.PackagePriceMin);
    }

    [Fact]
    public async Task CreatePackage_PackageTypeWithoutSessionCount_ThrowsValidationException_WithPackageSessionCountMin()
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"));
        await _db.SaveChangesAsync();

        var act = () => HandleAsync(ValidCommand("cat-1", "package") with { SessionCount = 0 });

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes["sessionCount"].Should().Contain(ApiErrorCodes.Packages.PackageSessionCountMin);
    }

    [Fact]
    public async Task CreatePackage_InvalidDiscountPercent_ThrowsValidationException_WithPackageDiscountPercentRange()
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"));
        await _db.SaveChangesAsync();

        var act = () => HandleAsync(ValidCommand() with { DiscountPercent = 150m });

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes["discountPercent"].Should().Contain(ApiErrorCodes.Packages.PackageDiscountPercentRange);
    }

    private async Task HandleAsync(CreatePackageCommand command)
    {
        var validationResult = await _validator.ValidateAsync(command);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var handler = new CreatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        await handler.Handle(command, default);
    }

    private static CreatePackageCommand ValidCommand(
        string serviceCategoryId = "cat-1",
        string offerType = "singleSession") =>
        new(
            "Summer Offer",
            true,
            offerType,
            "Full body session",
            serviceCategoryId,
            null,
            45,
            offerType == "package" ? 6 : 0,
            offerType == "package" ? 100 : null,
            offerType == "package" ? 12 : null,
            1500m,
            string.Empty,
            null);
}
