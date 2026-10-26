using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Customers.Queries.FindCustomerPurchases;

namespace Operia.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(IMediator mediator) : ControllerBase
{
    [HttpGet("inquiry")]
    [Authorize(Policy = Policies.CustomersRead)]
    public Task<CustomerPurchasesDto?> Inquiry(
        [FromQuery] string? mobile,
        CancellationToken cancellationToken) =>
        mediator.Send(new FindCustomerPurchasesQuery(mobile ?? string.Empty), cancellationToken);
}
