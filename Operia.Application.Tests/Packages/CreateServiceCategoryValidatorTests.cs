using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.Commands.CreateServiceCategory;
using Operia.Application.Tests.Helpers;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;
using Xunit;
using ValidationException = Operia.Application.Common.Exceptions.ValidationException;

namespace Operia.Application.Tests.Packages;

public sealed class CreateServiceCategoryValidatorTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly TestApplicationDbContext _db;
    private readonly IAuditWriter _auditWriter;
    private readonly CreateServiceCategoryValidator _validator = new();
    private string? _tenantId = "tenant-1";

    public CreateServiceCategoryValidatorTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("CreateServiceCategoryValidatorTests_" + Guid.NewGuid())
            .Options;
        _db = new TestApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _auditWriter = new AuditWriter(_db, _currentUserServiceMock.Object);
        _currentUserServiceMock.Setup(x => x.TenantId).Returns(() => _tenantId);
        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-1");
    }

    [Fact]
    public async Task CreateServiceCategory_EmptyName_ThrowsValidationException_WithCategoryNameRequired()
    {
        var act = () => HandleAsync(new CreateServiceCategoryCommand(string.Empty, "pi-tag"));

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes["name"].Should().Contain(ApiErrorCodes.Packages.CategoryNameRequired);
    }

    [Fact]
    public async Task CreateServiceCategory_NameTooLong_ThrowsValidationException_WithCategoryNameMax()
    {
        var act = () => HandleAsync(new CreateServiceCategoryCommand(new string('A', 201), "pi-tag"));

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes["name"].Should().Contain(ApiErrorCodes.Packages.CategoryNameMax);
    }

    [Fact]
    public async Task CreateServiceCategory_EmptyIcon_ThrowsValidationException_WithCategoryIconRequired()
    {
        var act = () => HandleAsync(new CreateServiceCategoryCommand("Body Laser", string.Empty));

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes["icon"].Should().Contain(ApiErrorCodes.Packages.CategoryIconRequired);
    }

    private async Task HandleAsync(CreateServiceCategoryCommand command)
    {
        var validationResult = await _validator.ValidateAsync(command);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var handler = new CreateServiceCategoryHandler(_db, _currentUserServiceMock.Object, _auditWriter);
        await handler.Handle(command, default);
    }
}
