using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Interfaces;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Infrastructure;

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task TenantScopedRecords_AreFilteredAndCannotBeWrittenAcrossTenants()
    {
        string? currentTenantId = null;
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(() => currentTenantId);

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(x => x.UtcNow).Returns(DateTime.UtcNow);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"TenantIsolationTests_{Guid.NewGuid()}")
            .Options;

        await using var db = new TestApplicationDbContext(
            options,
            dateTimeProvider.Object,
            currentUser.Object);

        db.Tenants.AddRange(
            new Tenant { Id = "tenant-a", OwnerUserId = "owner-a", BusinessName = "A" },
            new Tenant { Id = "tenant-b", OwnerUserId = "owner-b", BusinessName = "B" });
        db.Branches.AddRange(
            new Branch
            {
                Id = "branch-a",
                TenantId = "tenant-a",
                Name = "Tenant A Branch",
                Address = "A",
                PhoneNumber = "100",
                GoogleMapsUrl = "https://example.test/a"
            },
            new Branch
            {
                Id = "branch-b",
                TenantId = "tenant-b",
                Name = "Tenant B Branch",
                Address = "B",
                PhoneNumber = "200",
                GoogleMapsUrl = "https://example.test/b"
            });
        await db.SaveChangesAsync();

        currentTenantId = "tenant-a";

        var visibleBranches = await db.Branches
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync();

        visibleBranches.Should().ContainSingle();
        visibleBranches[0].Id.Should().Be("branch-a");

        var otherTenantBranch = await db.Branches
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == "branch-b");
        otherTenantBranch.Name = "Attempted cross-tenant update";

        await FluentActions.Invoking(() => db.SaveChangesAsync())
            .Should()
            .ThrowAsync<UnauthorizedAccessException>();
    }
}
