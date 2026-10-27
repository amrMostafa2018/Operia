namespace Operia.Application.Customers.Commands.CollectInstallment;

/// <summary>Serializes collection with booking reservations on the selected purchase.</summary>
public interface IInstallmentCollectionStore
{
    Task<CollectInstallmentResult> ExecuteAsync(
        string tenantId,
        string purchaseId,
        Func<CancellationToken, Task<CollectInstallmentResult>> collect,
        CancellationToken cancellationToken);
}
