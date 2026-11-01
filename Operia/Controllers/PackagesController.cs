using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Operia.Application.Auth;
using Operia.Application.Packages.Commands.CreatePackage;
using Operia.Application.Packages.Commands.CreateServiceCategory;
using Operia.Application.Packages.Commands.CreateSubServiceCategory;
using Operia.Application.Packages.Commands.DeletePackage;
using Operia.Application.Packages.Commands.UpdatePackage;
using Operia.Application.Packages.DTOs;
using Operia.Application.Packages.Queries.GetPackageById;
using Operia.Application.Packages.Queries.GetPackages;
using Operia.Application.Packages.Queries.GetServiceCategories;
using Operia.Application.Packages.Queries.GetSubServiceCategories;
using Operia.Application.Packages;
using Operia.Application.Packages.InstallmentPurchases;
using Operia.Application.Packages.Queries.GetAvailablePackageStaff;

namespace Operia.Controllers;

[ApiController]
[Route("api/packages")]
public sealed class PackagesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.PackagesRead)]
    public Task<PackageListResultDto> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? offerType = null,
        [FromQuery] string? serviceCategoryId = null,
        [FromQuery] string? status = null,
        [FromQuery] bool ignorePagination = false,
        CancellationToken cancellationToken = default) =>
        mediator.Send(
            new GetPackagesQuery(
                pageNumber,
                pageSize,
                search,
                offerType,
                serviceCategoryId,
                status,
                ignorePagination),
            cancellationToken);

    [HttpGet("service-categories")]
    [Authorize(Policy = Policies.PackagesRead)]
    public Task<IReadOnlyList<ServiceCategoryDto>> ListServiceCategories(
        CancellationToken cancellationToken) =>
        mediator.Send(new GetServiceCategoriesQuery(), cancellationToken);

    [HttpGet("sub-service-categories")]
    [Authorize(Policy = Policies.PackagesRead)]
    public Task<IReadOnlyList<SubServiceCategoryDto>> ListSubServiceCategories(
        [FromQuery] string? serviceCategoryId = null,
        CancellationToken cancellationToken = default) =>
        mediator.Send(new GetSubServiceCategoriesQuery(serviceCategoryId), cancellationToken);

    [HttpGet("available-staff")]
    [Authorize(Policy = Policies.PackagesRead)]
    public Task<IReadOnlyList<AvailablePackageStaffDto>> ListAvailableStaff(
        CancellationToken cancellationToken) =>
        mediator.Send(new GetAvailablePackageStaffQuery(), cancellationToken);

    [HttpGet("{id}")]
    [Authorize(Policy = Policies.PackagesRead)]
    public Task<PackageDetailDto> Get(string id, CancellationToken cancellationToken) =>
        mediator.Send(new GetPackageByIdQuery(id), cancellationToken);

    /// <summary>Reads a purchase's frozen installment schedule and collected amounts.</summary>
    [HttpGet("installment-purchases/{purchaseId}")]
    [Authorize(Policy = Policies.PackagesSell)]
    public Task<InstallmentPurchaseDto> GetInstallmentPurchase(
        string purchaseId, CancellationToken cancellationToken) =>
        mediator.Send(new GetInstallmentPurchaseQuery(purchaseId), cancellationToken);

    /// <summary>Confirms collection of the next scheduled payment and releases its sessions.</summary>
    [HttpPost("installment-purchases/{purchaseId}/installments/{number:int}/collect")]
    [Authorize(Policy = Policies.PackagesSell)]
    public Task<InstallmentPurchaseDto> CollectInstallment(
        string purchaseId, int number, CollectInstallmentRequest request,
        CancellationToken cancellationToken) =>
        mediator.Send(new CollectInstallmentCommand(
            purchaseId, number, request.Amount, request.PaymentMethod), cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.PackagesManage)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        CreatePackageCommand command,
        CancellationToken cancellationToken)
    {
        var package = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = package.Id }, package);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Policies.PackagesManage)]
    public Task<PackageDetailDto> Update(
        string id,
        UpdatePackageRequest request,
        CancellationToken cancellationToken) =>
        mediator.Send(
            new UpdatePackageCommand(
                id,
                request.Name,
                request.IsActive,
                request.OfferType,
                request.Description,
                request.ServiceCategoryId,
                request.SubServiceCategoryId,
                request.SessionDurationMinutes,
                request.SessionCount,
                request.PulseCount,
                request.PackageExpiryMonths,
                request.Price,
                request.DiscountCode,
                request.DiscountPercent,
                request.CashEnabled,
                request.InstallmentEnabled,
                request.InstallmentPrice,
                request.FirstPayment,
                request.InstallmentCount,
                request.Installments,
                request.AllStaff,
                request.StaffIds),
            cancellationToken);

    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.PackagesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeletePackageCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("service-categories")]
    [Authorize(Policy = Policies.PackagesManage)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateServiceCategory(
        CreateServiceCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var category = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(ListServiceCategories), category);
    }

    [HttpPost("sub-service-categories")]
    [Authorize(Policy = Policies.PackagesManage)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSubServiceCategory(
        CreateSubServiceCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var category = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(ListSubServiceCategories), category);
    }
}

