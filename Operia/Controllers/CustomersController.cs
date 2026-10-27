using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Customers.Queries.FindCustomerPurchases;
using Operia.Application.Customers.Queries.GetCustomerPurchaseDetails;
using Operia.Application.Customers.Commands.CollectInstallment;
using Operia.Application.Customers.Queries.ListDirectCustomerSales;

namespace Operia.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(IMediator mediator) : ControllerBase
{
    [HttpGet("direct-sales")]
    [Authorize(Policy = Policies.CustomersRead)]
    public Task<DirectCustomerSalesDto?> DirectSales([FromQuery] int page,
        CancellationToken cancellationToken) =>
        mediator.Send(new ListDirectCustomerSalesQuery(page), cancellationToken);

    [HttpGet("inquiry")]
    [Authorize(Policy = Policies.CustomersRead)]
    public Task<CustomerPurchasesDto?> Inquiry(
        [FromQuery] string? mobile,
        CancellationToken cancellationToken) =>
        mediator.Send(new FindCustomerPurchasesQuery(mobile ?? string.Empty), cancellationToken);

    [HttpGet("purchases/{purchaseId}/inquiry")]
    [Authorize(Policy = Policies.CustomersRead)]
    public Task<CustomerPurchaseDetailsDto> PurchaseInquiry(
        string purchaseId,
        CancellationToken cancellationToken) =>
        mediator.Send(new GetCustomerPurchaseDetailsQuery(purchaseId), cancellationToken);

    [HttpPost("purchases/{purchaseId}/installments")]
    [Authorize(Policy = Policies.CustomersCollectInstallment)]
    public Task<CollectInstallmentResult> CollectInstallment(
        string purchaseId,
        [FromBody] CollectInstallmentRequest request,
        CancellationToken cancellationToken) =>
        mediator.Send(new CollectInstallmentCommand(
            purchaseId, request.PaymentMethod, request.IdempotencyKey), cancellationToken);
}
