using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Operia.Application.Auth;
using Operia.Application.Common.Authorization;
using Operia.Infrastructure.Identity;
using Xunit;

namespace Operia.Application.Tests.Authorization;

public sealed class PermissionCatalogTests
{
    [Fact]
    public async Task V3RolesReceiveOnlyNewPermissionsOnce()
    {
        var names = new[]
        {
            Roles.SuperAdmin, Roles.Admin, Roles.Reception, Roles.Staff, Roles.PlatformAdmin
        };
        var claims = names.ToDictionary(
            name => name,
            _ => (IList<Claim>)new List<Claim>
            {
                new("operia.permission-baseline", "v3")
            });
        var store = new Mock<IRoleStore<IdentityRole>>();
        var manager = new Mock<RoleManager<IdentityRole>>(
            store.Object,
            Array.Empty<IRoleValidator<IdentityRole>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);
        manager.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => new IdentityRole(name));
        manager.Setup(x => x.GetClaimsAsync(It.IsAny<IdentityRole>()))
            .ReturnsAsync((IdentityRole role) => claims[role.Name!]);
        manager.Setup(x => x.AddClaimAsync(It.IsAny<IdentityRole>(), It.IsAny<Claim>()))
            .Callback((IdentityRole role, Claim claim) => claims[role.Name!].Add(claim))
            .ReturnsAsync(IdentityResult.Success);
        manager.Setup(x => x.RemoveClaimAsync(It.IsAny<IdentityRole>(), It.IsAny<Claim>()))
            .Callback((IdentityRole role, Claim claim) => claims[role.Name!].Remove(claim))
            .ReturnsAsync(IdentityResult.Success);

        await PermissionClaimBootstrapper.EnsureBaselinePermissionsAsync(manager.Object);
        await PermissionClaimBootstrapper.EnsureBaselinePermissionsAsync(manager.Object);

        foreach (var name in names)
        {
            Assert.Single(claims[name], claim => claim.Type == "operia.permission-baseline" &&
                                                 claim.Value == "v6");
            var permissions = claims[name]
                .Where(claim => claim.Type == Policies.PermissionClaimType)
                .Select(claim => claim.Value)
                .ToArray();
            if (name is Roles.SuperAdmin or Roles.Admin)
            {
                Assert.Equal(
                    [Policies.CustomersCollectInstallment, Policies.CustomersRefund,
                        Policies.CustomersBlock, Policies.CustomersHistory], permissions);
            }
            else if (name is Roles.Reception)
            {
                Assert.Equal([Policies.CustomersCollectInstallment], permissions);
            }
            else if (name is Roles.Staff)
            {
                Assert.Equal([Policies.BookingsChangeStatus], permissions);
            }
            else
            {
                Assert.Empty(permissions);
            }
        }
    }

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
            Policies.CustomersCollectInstallment,
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
            Policies.BookingsChangeStatus,
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
