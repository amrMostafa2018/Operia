namespace Operia.Application.Customers.Commands.CancelAndRefundPurchase;

/// <summary>Serializes a purchase refund with booking reservation changes.</summary>
public interface ICustomerRefundStore
{
    Task<CancelAndRefundPurchaseResult> ExecuteAsync(
        string tenantId, string purchaseId,
        Func<CancellationToken, Task<CancelAndRefundPurchaseResult>> post,
        CancellationToken cancellationToken);
}
