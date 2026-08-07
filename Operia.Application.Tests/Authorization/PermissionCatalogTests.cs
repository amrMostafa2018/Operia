using Operia.Application.Auth;
using Operia.Application.Common.Authorization;
using Operia.Infrastructure.Identity;
using Xunit;

namespace Operia.Application.Tests.Authorization;

public sealed class PermissionCatalogTests
{
    [Fact]
    public void PermissionCatalog_HasNoDuplicateValues()
    {
        var duplicates = Policies.PermissionValues
            .GroupBy(permission => permission, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(duplicates);
    }

    [Theory]
    [InlineData(Roles.SuperAdmin)]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Reception)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.PlatformAdmin)]
    public void BaselineRolePermissions_ContainOnlyRegisteredPolicies(string roleName)
    {
        var permissions = PermissionClaimBootstrapper.GetBaselinePermissions(roleName);

        Assert.NotEmpty(permissions);
        Assert.All(permissions, permission => Assert.Contains(permission, Policies.PermissionValues));
    }

    [Fact]
    public void PlatformAdmin_BaselineHasOnlyThePlatformPermission()
    {
        var permissions = PermissionClaimBootstrapper.GetBaselinePermissions(Roles.PlatformAdmin);

        Assert.Equal([Policies.Platform.Manage], permissions);
    }
}
