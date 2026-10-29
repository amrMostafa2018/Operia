namespace Operia.Application.Sales.Commands.CollectSale;

/// <summary>Serializes collection attempts on the saved sale.</summary>
public interface ISaleCollectionStore
{
    Task<TResult> ExecuteAsync<TResult>(
        string tenantId,
        string saleId,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken);

    Task<CollectSaleResult> ExecuteAsync(
        string tenantId,
        string saleId,
        Func<CancellationToken, Task<CollectSaleResult>> collect,
        CancellationToken cancellationToken);
}
