using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Interfaces;
using Operia.Application.Tests.Helpers;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Customers;

public sealed class InstallmentCollectionSqlTests
{
    [LocalE2EFact]
    public async Task Store_RollsBackSavedPurchaseWhenWorkFailsBeforeCommit()
    {
        const string tenantId = "e2e70000-0000-4000-8000-000000000001";
        const string purchaseId = "e2e70000-0000-4000-8000-000000000082";
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns(tenantId);
        user.SetupGet(x => x.UserId).Returns("e2e-rollback-test");
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\OperiaDev;Database=Operia_Dev;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        await using var db = new TestApplicationDbContext(options, clock.Object, user.Object);
        var before = await db.CustomerPackages.AsNoTracking()
            .Where(x => x.Id == purchaseId && x.TenantId == tenantId)
            .Select(x => x.AmountDue)
            .SingleAsync();
        var store = new InstallmentCollectionStore(db);
        var operation = () => store.ExecuteAsync<int>(tenantId, purchaseId, async token =>
        {
            var purchase = await db.CustomerPackages.SingleAsync(x => x.Id == purchaseId, token);
            purchase.AmountDue = before + 1m;
            await db.SaveChangesAsync(token);
            throw new Exception("Simulated failure after save");
        }, CancellationToken.None);

        await operation.Should().ThrowAsync<Exception>().WithMessage("Simulated failure after save");
        await using var verification = new TestApplicationDbContext(options, clock.Object, user.Object);
        var after = await verification.CustomerPackages.AsNoTracking()
            .Where(x => x.Id == purchaseId && x.TenantId == tenantId)
            .Select(x => x.AmountDue)
            .SingleAsync();
        after.Should().Be(before);
    }

    private sealed class LocalE2EFactAttribute : FactAttribute
    {
        public LocalE2EFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("OPERIA_US11_SQL_TEST") != "1")
            {
                Skip = "Requires the reserved LocalDB E2E purchase and an idle E2E database.";
            }
        }
    }
}
