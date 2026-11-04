using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Sales.Queries.QuoteSale;
using Operia.Application.Sales.Commands.ConfirmSale;
using Operia.Application.Bookings.Queries.FindBookingCustomer;
using Operia.Application.Bookings.Queries.GetBookingPaymentMethods;
using Operia.Application.Branches.Queries.GetBookingBranches;
using Operia.Application.Packages.Queries.GetPackages;
using Operia.Application.Packages.Queries.GetServiceCategories;
using Operia.Application.Packages.DTOs;

namespace Operia.Controllers;

[ApiController]
[Route("api/sales")]
public sealed class SalesController(IMediator mediator) : ControllerBase
{
    [HttpGet("catalog")]
    [Authorize(Policy = Policies.PackagesSell)]
    public Task<PackageListResultDto> Catalog(CancellationToken cancellationToken) =>
        mediator.Send(new GetPackagesQuery(1, 1, null, null, null, "active", true), cancellationToken);

    [HttpGet("categories")]
    [Authorize(Policy = Policies.PackagesSell)]
    public Task<IReadOnlyList<ServiceCategoryDto>> Categories(CancellationToken cancellationToken) =>
        mediator.Send(new GetServiceCategoriesQuery(), cancellationToken);

    [HttpGet("branches")]
    [Authorize(Policy = Policies.PackagesSell)]
    public Task<IReadOnlyList<BookingBranchDto>> Branches(CancellationToken cancellationToken) =>
        mediator.Send(new GetBookingBranchesQuery(), cancellationToken);

    [HttpGet("customer")]
    [Authorize(Policy = Policies.PackagesSell)]
    public Task<BookingCustomerDto?> Customer([FromQuery] string mobile,
        CancellationToken cancellationToken) =>
        mediator.Send(new FindBookingCustomerQuery(mobile), cancellationToken);

    [HttpGet("payment-methods")]
    [Authorize(Policy = Policies.PackagesSell)]
    public Task<IReadOnlyList<string>> PaymentMethods(CancellationToken cancellationToken) =>
        mediator.Send(new GetBookingPaymentMethodsQuery(), cancellationToken);

    [HttpPost("quote")]
    [Authorize(Policy = Policies.PackagesSell)]
    public Task<SaleQuoteDto> Quote(QuoteSaleQuery query, CancellationToken cancellationToken) =>
        mediator.Send(query, cancellationToken);

    [HttpPost("confirm")]
    [Authorize(Policy = Policies.PackagesSell)]
    public Task<ConfirmSaleResult> Confirm(ConfirmSaleCommand command, CancellationToken cancellationToken) =>
        mediator.Send(command, cancellationToken);
}
