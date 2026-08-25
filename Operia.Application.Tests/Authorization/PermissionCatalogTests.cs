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
    public void SuperAdmin_BaselineHasAllTenantPermissions()
    {
        var permissions = PermissionClaimBootstrapper.GetBaselinePermissions(Roles.SuperAdmin);

        Assert.Equal(Policies.TenantPermissionValues, permissions);
    }

    [Fact]
    public void Admin_BaselineExcludesRestrictedPermissions()
    {
        var permissions = PermissionClaimBootstrapper.GetBaselinePermissions(Roles.Admin);

        var expected = Policies.TenantPermissionValues
            .Where(permission => !PermissionClaimBootstrapper.AdminExcludedPermissions.Contains(permission))
            .ToArray();

        Assert.Equal(expected, permissions);
    }

    [Fact]
    public void Reception_BaselineMatchesOperationalMatrix()
    {
        var permissions = PermissionClaimBootstrapper.GetBaselinePermissions(Roles.Reception);

        Assert.Equal(
        [
            Policies.DashboardRead,
            Policies.DashboardExport,
            Policies.BookingsRead,
            Policies.BookingsManage,
            Policies.BookingsExport,
            Policies.BookingsCancel,
            Policies.BookingsReassign,
            Policies.BookingsChangeStatus,
            Policies.CustomersRead,
            Policies.CustomersManage,
            Policies.CustomersExport,
            Policies.CustomersActivatePackage,
            Policies.CustomersCancelPackage,
            Policies.CustomersAddPackage,
            Policies.PackagesRead,
            Policies.PackagesSell,
            Policies.OffersRead,
            Policies.BranchesRead,
            Policies.SupportRead,
            Policies.NotificationsRead
        ], permissions);
    }

    [Fact]
    public void Staff_BaselineMatchesOperationalMatrix()
    {
        var permissions = PermissionClaimBootstrapper.GetBaselinePermissions(Roles.Staff);

        Assert.Equal(
        [
            Policies.BookingsRead,
            Policies.SupportRead,
            Policies.NotificationsRead
        ], permissions);
    }

    [Fact]
    public void PlatformAdmin_BaselineHasOnlyThePlatformPermission()
    {
        var permissions = PermissionClaimBootstrapper.GetBaselinePermissions(Roles.PlatformAdmin);

        Assert.Equal([Policies.Platform.Manage], permissions);
    }
}
