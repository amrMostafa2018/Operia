using System.Data;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Sales.Commands.CollectSale;

namespace Operia.Infrastructure.Persistence;

public sealed class SaleCollectionStore(ApplicationDbContext db) : ISaleCollectionStore
{
    public async Task<CollectSaleResult> ExecuteAsync(
        string tenantId,
        string saleId,
        Func<CancellationToken, Task<CollectSaleResult>> collect,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync<CollectSaleResult>(tenantId, saleId, collect,
            cancellationToken);
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        string tenantId,
        string saleId,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
        {
            return await action(cancellationToken);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC dbo.AcquireBookingMutationLock @Resource={"sale-collection:" + tenantId + ":" + saleId}",
            cancellationToken);
        var result = await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
