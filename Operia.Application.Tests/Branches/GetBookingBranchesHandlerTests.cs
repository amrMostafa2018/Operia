using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Branches.Queries.GetBookingBranches;
using Operia.Application.Common.Interfaces;
using Operia.Application.Tests.Helpers;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Branches;

/// <summary>Verifies that booking branch lookup follows tenant and branch scope.</summary>
public sealed class GetBookingBranchesHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReturnsOnlyAllowedTenantBranchesOrEmpty(bool hasAssignment)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.UserId).Returns("operator");
        string? tenant = null;
        user.SetupGet(x => x.TenantId).Returns(() => tenant);
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new TestApplicationDbContext(options, clock.Object, user.Object);
        db.Branches.AddRange(
            BranchTestData.Branch("a", "tenant-1", "business-1", "A"),
            BranchTestData.Branch("b", "tenant-1", "business-1", "B"),
            BranchTestData.Branch("foreign", "tenant-2", "business-2", "Other"));
        await db.SaveChangesAsync();
        tenant = "tenant-1";
        var scope = new Mock<IBranchScope>();
        scope.Setup(x => x.GetAllowedBranchIdsAsync("operator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasAssignment ? new[] { "b", "foreign" } : Array.Empty<string>());

        var result = await new GetBookingBranchesHandler(db, user.Object, scope.Object)
            .Handle(new GetBookingBranchesQuery(), CancellationToken.None);

        result.Select(x => x.Id).Should().BeEquivalentTo(hasAssignment ? new[] { "b" } : Array.Empty<string>());
    }
}
