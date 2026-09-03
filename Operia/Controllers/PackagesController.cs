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
        CancellationToken cancellationToken = default) =>
        mediator.Send(
            new GetPackagesQuery(pageNumber, pageSize, search, offerType, serviceCategoryId, status),
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

    [HttpGet("{id}")]
    [Authorize(Policy = Policies.PackagesRead)]
    public Task<PackageDetailDto> Get(string id, CancellationToken cancellationToken) =>
        mediator.Send(new GetPackageByIdQuery(id), cancellationToken);

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
                request.DiscountPercent),
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

