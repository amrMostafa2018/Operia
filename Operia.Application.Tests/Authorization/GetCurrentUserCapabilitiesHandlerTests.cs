using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Auth.Queries.GetCurrentUserCapabilities;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Authorization;

public sealed class GetCurrentUserCapabilitiesHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserService = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();
    private readonly Mock<IPermissionGrantStore> _permissionStore = new();
    private readonly TestApplicationDbContext _db;

    public GetCurrentUserCapabilitiesHandlerTests()
    {
        _dateTimeProvider.Setup(provider => provider.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("GetCurrentUserCapabilitiesTests_" + Guid.NewGuid())
            .Options;
        _db = new TestApplicationDbContext(options, _dateTimeProvider.Object, _currentUserService.Object);

        _currentUserService.SetupGet(service => service.UserId).Returns("user-1");
        _permissionStore
            .Setup(store => store.GetUserCapabilitiesAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionGrantSnapshot(
                ["Admin"],
                ["Dashboard.Read", "Employees.Read"]));
    }

    [Fact]
    public async Task ReturnsTheCurrentUsersPersistedCapabilities()
    {
        _currentUserService.SetupGet(service => service.TenantId).Returns((string?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetCurrentUserCapabilitiesQuery(), CancellationToken.None);

        result.Roles.Should().Equal("Admin");
        result.Permissions.Should().Equal("Dashboard.Read", "Employees.Read");
        result.CurrencyCode.Should().BeNull();
    }

    [Fact]
    public async Task ReturnsTenantCurrencyCode_WhenTenantExists()
    {
        _currentUserService.SetupGet(service => service.TenantId).Returns("tenant-1");
        _db.Tenants.Add(new Tenant
        {
            Id = "tenant-1",
            OwnerUserId = "user-1",
            CurrencyCode = "EGP",
        });
        await _db.SaveChangesAsync();

        var handler = CreateHandler();
        var result = await handler.Handle(new GetCurrentUserCapabilitiesQuery(), CancellationToken.None);

        result.CurrencyCode.Should().Be("EGP");
    }

    [Fact]
    public async Task ReturnsNullCurrencyCode_WhenTenantIdIsMissing()
    {
        _currentUserService.SetupGet(service => service.TenantId).Returns((string?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetCurrentUserCapabilitiesQuery(), CancellationToken.None);

        result.CurrencyCode.Should().BeNull();
    }

    [Fact]
    public async Task ReturnsNullCurrencyCode_WhenTenantRowIsMissing()
    {
        _currentUserService.SetupGet(service => service.TenantId).Returns("missing-tenant");
        var handler = CreateHandler();

        var result = await handler.Handle(new GetCurrentUserCapabilitiesQuery(), CancellationToken.None);

        result.CurrencyCode.Should().BeNull();
    }

    [Fact]
    public async Task MissingCurrentUser_ThrowsUnauthorizedWithoutQueryingTheStore()
    {
        _currentUserService.SetupGet(service => service.UserId).Returns((string?)null);
        var handler = CreateHandler();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new GetCurrentUserCapabilitiesQuery(), CancellationToken.None));

        _permissionStore.Verify(
            store => store.GetUserCapabilitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private GetCurrentUserCapabilitiesHandler CreateHandler() =>
        new(_currentUserService.Object, _permissionStore.Object, _db);
}
