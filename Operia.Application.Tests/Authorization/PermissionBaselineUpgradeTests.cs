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
    [Fact]
    public async Task V3RolesReceiveOnlyTheNewCollectionPermission()
    {
        var roleNames = new[]
        {
            Roles.SuperAdmin, Roles.Admin, Roles.Reception, Roles.Staff, Roles.PlatformAdmin
        };
        var roles = roleNames.ToDictionary(name => name, name => new IdentityRole(name));
        var claims = roleNames.ToDictionary(name => name, name => (IList<Claim>)
        [
            new Claim("operia.permission-baseline", "v3")
        ]);
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
                                                 claim.Value == "v4");
            Assert.Equal(name is Roles.SuperAdmin or Roles.Admin or Roles.Reception ? 1 : 0,
                claims[name].Count(claim => claim.Type == Policies.PermissionClaimType &&
                                            claim.Value == Policies.CustomersCollectInstallment));
        }
        Assert.DoesNotContain(claims[Roles.Reception], claim =>
            claim.Type == Policies.PermissionClaimType && claim.Value == Policies.BookingsExport);
    }
}
