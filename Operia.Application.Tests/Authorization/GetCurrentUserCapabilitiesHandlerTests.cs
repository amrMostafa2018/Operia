using Moq;
using Operia.Application.Auth.Queries.GetCurrentUserCapabilities;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Authorization;

public sealed class GetCurrentUserCapabilitiesHandlerTests
{
    [Fact]
    public async Task ReturnsTheCurrentUsersPersistedCapabilities()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.UserId).Returns("user-1");

        var permissionStore = new Mock<IPermissionGrantStore>();
        permissionStore.Setup(store => store.GetUserCapabilitiesAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionGrantSnapshot(
                ["Admin"],
                ["Dashboard.Read", "Employees.Read"]));

        var handler = new GetCurrentUserCapabilitiesHandler(currentUser.Object, permissionStore.Object);

        var result = await handler.Handle(new GetCurrentUserCapabilitiesQuery(), CancellationToken.None);

        Assert.Equal(["Admin"], result.Roles);
        Assert.Equal(["Dashboard.Read", "Employees.Read"], result.Permissions);
    }

    [Fact]
    public async Task MissingCurrentUser_ThrowsUnauthorizedWithoutQueryingTheStore()
    {
        var currentUser = new Mock<ICurrentUserService>();
        var permissionStore = new Mock<IPermissionGrantStore>();
        var handler = new GetCurrentUserCapabilitiesHandler(currentUser.Object, permissionStore.Object);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new GetCurrentUserCapabilitiesQuery(), CancellationToken.None));

        permissionStore.Verify(
            store => store.GetUserCapabilitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
