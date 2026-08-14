using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Exceptions;
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
        db.Businesses.AddRange(
            BranchTestData.Business("tenant-a", "business-a"),
            BranchTestData.Business("tenant-b", "business-b"));
        db.Branches.AddRange(
            BranchTestData.Branch("branch-a", "tenant-a", "business-a", "Tenant A Branch", "A", "100"),
            BranchTestData.Branch("branch-b", "tenant-b", "business-b", "Tenant B Branch", "B", "200"));
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
            .ThrowAsync<ForbiddenException>();
    }
}
