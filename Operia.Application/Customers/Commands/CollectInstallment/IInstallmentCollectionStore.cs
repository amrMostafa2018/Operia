namespace Operia.Application.Customers.Commands.CollectInstallment;

/// <summary>Serializes collection with booking reservations on the selected purchase.</summary>
public interface IInstallmentCollectionStore
{
    Task<TResult> ExecuteAsync<TResult>(
        string tenantId,
        string purchaseId,
        Func<CancellationToken, Task<TResult>> collect,
        CancellationToken cancellationToken);
}
