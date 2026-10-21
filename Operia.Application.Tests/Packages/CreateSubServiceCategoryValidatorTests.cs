using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.Commands.CreateSubServiceCategory;
using Operia.Application.Tests.Helpers;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;
using Xunit;
using ValidationException = Operia.Application.Common.Exceptions.ValidationException;

namespace Operia.Application.Tests.Packages;

public sealed class CreateSubServiceCategoryValidatorTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly TestApplicationDbContext _db;
    private readonly IAuditWriter _auditWriter;
    private readonly CreateSubServiceCategoryValidator _validator = new();
    private string? _tenantId = "tenant-1";

    public CreateSubServiceCategoryValidatorTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("CreateSubServiceCategoryValidatorTests_" + Guid.NewGuid())
            .Options;
        _db = new TestApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _auditWriter = new AuditWriter(_db, _currentUserServiceMock.Object);
        _currentUserServiceMock.Setup(x => x.TenantId).Returns(() => _tenantId);
        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-1");
    }

    [Fact]
    public async Task CreateSubServiceCategory_EmptyName_ThrowsValidationException_WithSubCategoryNameRequired()
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"));
        await _db.SaveChangesAsync();

        var act = () => HandleAsync(new CreateSubServiceCategoryCommand(string.Empty, "cat-1"));

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes["name"].Should().Contain(ApiErrorCodes.Packages.SubCategoryNameRequired);
    }

    [Fact]
    public async Task CreateSubServiceCategory_NameTooLong_ThrowsValidationException_WithSubCategoryNameMax()
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory("cat-1", "tenant-1", "Body Laser"));
        await _db.SaveChangesAsync();

        var act = () => HandleAsync(new CreateSubServiceCategoryCommand(new string('A', 201), "cat-1"));

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes["name"].Should().Contain(ApiErrorCodes.Packages.SubCategoryNameMax);
    }

    [Fact]
    public async Task CreateSubServiceCategory_EmptyServiceCategoryId_ThrowsValidationException_WithSubCategoryParentRequired()
    {
        var act = () => HandleAsync(new CreateSubServiceCategoryCommand("Upper Body", string.Empty));

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes["serviceCategoryId"].Should().Contain(ApiErrorCodes.Packages.SubCategoryParentRequired);
    }

    private async Task HandleAsync(CreateSubServiceCategoryCommand command)
    {
        var validationResult = await _validator.ValidateAsync(command);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var handler = new CreateSubServiceCategoryHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        await handler.Handle(command, default);
    }
}
