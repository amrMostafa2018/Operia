using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Operia.Application.Auth;
using Operia.Application.Common.Authorization;
using Operia.Infrastructure.Authorization;
using Xunit;

namespace Operia.Application.Tests.Authorization;

public sealed class DatabasePermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task PermissionStoredInIdentityTables_AllowsTheRequirement()
    {
        var store = new Mock<IPermissionGrantStore>();
        store.Setup(x => x.HasPermissionAsync("user-1", Policies.SubscriptionsRead, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var requirement = new PermissionRequirement(Policies.SubscriptionsRead);
        var context = CreateContext(requirement, "user-1");

        await new DatabasePermissionAuthorizationHandler(store.Object).HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task MissingDatabasePermission_DeniesTheRequirement()
    {
        var store = new Mock<IPermissionGrantStore>();
        store.Setup(x => x.HasPermissionAsync("user-1", Policies.SubscriptionsRead, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var requirement = new PermissionRequirement(Policies.SubscriptionsRead);
        var context = CreateContext(requirement, "user-1");

        await new DatabasePermissionAuthorizationHandler(store.Object).HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task MissingUserIdentifier_DoesNotQueryThePermissionStore()
    {
        var store = new Mock<IPermissionGrantStore>();
        var requirement = new PermissionRequirement(Policies.SubscriptionsRead);
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity("TestAuth")),
            resource: null);

        await new DatabasePermissionAuthorizationHandler(store.Object).HandleAsync(context);

        Assert.False(context.HasSucceeded);
        store.Verify(
            x => x.HasPermissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static AuthorizationHandlerContext CreateContext(
        PermissionRequirement requirement,
        string userId) =>
        new(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                "TestAuth")),
            resource: null);
}
