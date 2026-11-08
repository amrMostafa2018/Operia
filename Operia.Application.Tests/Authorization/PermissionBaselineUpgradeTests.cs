using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Operia.Application.Auth;
using Operia.Application.Common.Authorization;
using Operia.Infrastructure.Identity;
using Xunit;

namespace Operia.Application.Tests.Authorization;

public sealed class PermissionBaselineUpgradeTests
{
    [Theory]
    [InlineData("v3")]
    [InlineData("v4")]
    [InlineData("v1")]
    [InlineData("v5")]
    public async Task EarlierRolesReceiveOnlyNewPermissions(string previousVersion)
    {
        var roleNames = new[]
        {
            Roles.SuperAdmin, Roles.Admin, Roles.Reception, Roles.Staff, Roles.PlatformAdmin
        };
        var roles = roleNames.ToDictionary(name => name, name => new IdentityRole(name));
        var claims = roleNames.ToDictionary(name => name, name => (IList<Claim>)
        [
            new Claim("operia.permission-baseline", previousVersion)
        ]);
        if (previousVersion == "v3")
        {
            foreach (var roleClaims in claims.Values)
            {
                roleClaims.Insert(0, new Claim("operia.permission-baseline", "v1"));
            }
        }
        // A deliberate removal from the previous baseline must remain removed.
        claims[Roles.Reception].Add(new Claim(Policies.PermissionClaimType, Policies.BookingsRead));

        var manager = new Mock<RoleManager<IdentityRole>>(
            new Mock<IRoleStore<IdentityRole>>().Object,
            Array.Empty<IRoleValidator<IdentityRole>>(),
            new Mock<ILookupNormalizer>().Object,
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);
        manager.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
            .Returns((string name) => Task.FromResult<IdentityRole?>(roles[name]));
        manager.Setup(x => x.GetClaimsAsync(It.IsAny<IdentityRole>()))
            .Returns((IdentityRole role) => Task.FromResult(claims[role.Name!]));
        manager.Setup(x => x.AddClaimAsync(It.IsAny<IdentityRole>(), It.IsAny<Claim>()))
            .Returns((IdentityRole role, Claim claim) =>
            {
                claims[role.Name!].Add(claim);
                return Task.FromResult(IdentityResult.Success);
            });
        manager.Setup(x => x.RemoveClaimAsync(It.IsAny<IdentityRole>(), It.IsAny<Claim>()))
            .Returns((IdentityRole role, Claim claim) =>
            {
                claims[role.Name!].Remove(claim);
                return Task.FromResult(IdentityResult.Success);
            });

        await PermissionClaimBootstrapper.EnsureBaselinePermissionsAsync(manager.Object);
        await PermissionClaimBootstrapper.EnsureBaselinePermissionsAsync(manager.Object);

        foreach (var name in roleNames)
        {
            Assert.Single(claims[name], claim => claim.Type == "operia.permission-baseline" &&
                                                 claim.Value == "v6");
            Assert.Equal(previousVersion != "v5" && (name is Roles.SuperAdmin or Roles.Admin or Roles.Reception) ? 1 : 0,
                claims[name].Count(claim => claim.Type == Policies.PermissionClaimType &&
                                            claim.Value == Policies.CustomersCollectInstallment));
        }
        Assert.DoesNotContain(claims[Roles.Reception], claim =>
            claim.Type == Policies.PermissionClaimType && claim.Value == Policies.BookingsExport);
        foreach (var role in previousVersion == "v5" ? Array.Empty<string>() : new[] { Roles.SuperAdmin, Roles.Admin })
        {
            foreach (var permission in new[] { Policies.CustomersRefund, Policies.CustomersBlock,
                         Policies.CustomersHistory })
            {
                Assert.Single(claims[role], claim =>
                    claim.Type == Policies.PermissionClaimType && claim.Value == permission);
            }
        }

        if (previousVersion == "v5") return;
        var removedGrant = claims[Roles.Reception].Single(claim =>
            claim.Type == Policies.PermissionClaimType && claim.Value == Policies.CustomersCollectInstallment);
        claims[Roles.Reception].Remove(removedGrant);
        claims[Roles.Reception].Add(new Claim("operia.permission-baseline", "v1"));
        await PermissionClaimBootstrapper.EnsureBaselinePermissionsAsync(manager.Object);
        Assert.DoesNotContain(claims[Roles.Reception], claim =>
            claim.Type == Policies.PermissionClaimType && claim.Value == Policies.CustomersCollectInstallment);
    }
}
