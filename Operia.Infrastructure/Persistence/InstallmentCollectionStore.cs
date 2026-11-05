using System.Data;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Customers.Commands.CollectInstallment;

namespace Operia.Infrastructure.Persistence;

public sealed class InstallmentCollectionStore(ApplicationDbContext db) : IInstallmentCollectionStore
{
    public async Task<TResult> ExecuteAsync<TResult>(
        string tenantId,
        string purchaseId,
        Func<CancellationToken, Task<TResult>> collect,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
        {
            return await collect(cancellationToken);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC dbo.AcquireBookingMutationLock @Resource={"booking-package:" + tenantId + ":" + purchaseId}",
            cancellationToken);
        var result = await collect(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
