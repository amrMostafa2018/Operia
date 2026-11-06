using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Customers.Queries.FindCustomerPurchases;
using Operia.Application.Customers.Queries.GetCustomerPurchaseDetails;
using Operia.Application.Customers.Commands.CollectInstallment;
using Operia.Application.Customers.Queries.ListDirectCustomerSales;
using Operia.Application.Customers.Commands.RegisterBookingCustomer;
using Operia.Application.Customers.Queries.ListCustomerOperations;
using Operia.Application.Customers.Queries.ExportCustomerOperations;
using Operia.Application.Customers.Queries.GetCustomerStatement;
using Operia.Application.Customers.Queries.GetCustomerAction;
using Operia.Application.Customers.Commands.CancelAndRefundPurchase;
using Operia.Application.Customers.Commands.SetCustomerBlock;
using Operia.Application.Customers.Queries.GetCustomerActionHistory;

namespace Operia.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(IMediator mediator) : ControllerBase
{
    [HttpGet("operations")]
    [Authorize(Policy = Policies.CustomersRead)]
    public Task<CustomerOperationsPageDto> Operations(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? mobile = null,
        [FromQuery] string? service = null,
        [FromQuery] string? serviceType = null,
        [FromQuery] string? status = null,
        [FromQuery] string? paymentType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        mediator.Send(new ListCustomerOperationsQuery(
            new CustomerOperationsFilter(fromDate, toDate, customer, mobile, service,
                serviceType, status, paymentType), page, pageSize), cancellationToken);

    [HttpGet("operations/export")]
    [Authorize(Policy = Policies.CustomersExport)]
    public async Task<FileContentResult> ExportOperations(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? mobile = null,
        [FromQuery] string? service = null,
        [FromQuery] string? serviceType = null,
        [FromQuery] string? status = null,
        [FromQuery] string? paymentType = null,
        CancellationToken cancellationToken = default)
    {
        var workbook = await mediator.Send(new ExportCustomerOperationsQuery(
            new CustomerOperationsFilter(fromDate, toDate, customer, mobile, service,
                serviceType, status, paymentType)), cancellationToken);
        return File(workbook,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "customer-operations.xlsx");
    }

    [HttpPost("registration")]
    [Authorize(Policy = Policies.CustomersManage)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterBookingCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var customer = await mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, customer);
    }

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

    [HttpGet("{customerId}/purchases/{purchaseId}/statement")]
    [Authorize(Policy = Policies.CustomersRead)]
    public Task<CustomerStatementDto> Statement(
        string customerId,
        string purchaseId,
        CancellationToken cancellationToken) =>
        mediator.Send(new GetCustomerStatementQuery(customerId, purchaseId), cancellationToken);

    [HttpPost("purchases/{purchaseId}/installments")]
    [Authorize(Policy = Policies.CustomersCollectInstallment)]
    public Task<CollectInstallmentResult> CollectInstallment(
        string purchaseId,
        [FromBody] CollectInstallmentRequest request,
        CancellationToken cancellationToken) =>
        mediator.Send(new CollectInstallmentCommand(
            purchaseId, request.PaymentMethod, request.IdempotencyKey), cancellationToken);

    [HttpGet("{customerId}/purchases/{purchaseId}/action")]
    [Authorize(Policy = Policies.CustomersRead)]
    public Task<CustomerActionDto> Action(string customerId, string purchaseId,
        CancellationToken cancellationToken) =>
        mediator.Send(new GetCustomerActionQuery(customerId, purchaseId), cancellationToken);

    [HttpGet("{customerId}/sales/{saleId}/action")]
    [Authorize(Policy = Policies.CustomersRead)]
    public Task<CustomerBlockActionDto> SaleAction(string customerId, string saleId,
        CancellationToken cancellationToken) =>
        mediator.Send(new GetCustomerSaleActionQuery(customerId, saleId), cancellationToken);

    [HttpPost("purchases/{purchaseId}/cancel-refund")]
    [Authorize(Policy = Policies.CustomersRefund)]
    public Task<CancelAndRefundPurchaseResult> CancelAndRefund(string purchaseId,
        [FromBody] CancelAndRefundPurchaseRequest request, CancellationToken cancellationToken) =>
        mediator.Send(new CancelAndRefundPurchaseCommand(purchaseId, request.Amount,
            request.IdempotencyKey, request.Note), cancellationToken);

    [HttpPut("{customerId}/block")]
    [Authorize(Policy = Policies.CustomersBlock)]
    public Task<SetCustomerBlockResult> SetBlock(string customerId,
        [FromBody] SetCustomerBlockRequest request, CancellationToken cancellationToken) =>
        mediator.Send(new SetCustomerBlockCommand(customerId, request.IsBlocked,
            request.Reason, request.Version), cancellationToken);

    [HttpGet("{customerId}/purchases/{purchaseId}/history")]
    [Authorize(Policy = Policies.CustomersHistory)]
    public Task<CustomerActionHistoryPage> History(string customerId, string purchaseId,
        [FromQuery] string? cursor = null, CancellationToken cancellationToken = default) =>
        mediator.Send(new GetCustomerActionHistoryQuery(customerId, purchaseId, false, cursor),
            cancellationToken);

    [HttpGet("{customerId}/sales/{saleId}/history")]
    [Authorize(Policy = Policies.CustomersHistory)]
    public Task<CustomerActionHistoryPage> SaleHistory(string customerId, string saleId,
        [FromQuery] string? cursor = null, CancellationToken cancellationToken = default) =>
        mediator.Send(new GetCustomerActionHistoryQuery(customerId, saleId, true, cursor),
            cancellationToken);
}
