using Microsoft.AspNetCore.Authorization;
using Operia.Application.Auth;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Branches;
using Operia.Application.Branches.Commands.CreateBranch;
using Operia.Application.Branches.Commands.DeleteBranch;
using Operia.Application.Branches.Commands.UpdateBranch;
using Operia.Application.Branches.Queries.GetBranch;
using Operia.Application.Branches.Queries.ListBranches;

namespace Operia.Controllers;

[ApiController]
[Route("api/branches")]
public sealed class BranchesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.BranchesRead)]
    public Task<BranchListResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default) =>
        mediator.Send(new ListBranchesQuery(pageNumber, pageSize, search, sortBy, sortDirection), cancellationToken);

    [HttpGet("{id}")]
    [Authorize(Policy = Policies.BranchesRead)]
    public Task<BranchDto> Get(string id, CancellationToken cancellationToken) =>
        mediator.Send(new GetBranchQuery(id), cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.BranchesManage)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateBranchCommand command, CancellationToken cancellationToken)
    {
        var branch = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = branch.Id }, branch);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Policies.BranchesManage)]
    public Task<BranchDto> Update(string id, UpdateBranchRequest request, CancellationToken cancellationToken) =>
        mediator.Send(new UpdateBranchCommand(id, request.Name, request.Address, request.PhoneNumber, request.Latitude, request.Longitude), cancellationToken);

    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.BranchesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteBranchCommand(id), cancellationToken);
        return NoContent();
    }
}

public sealed record UpdateBranchRequest(string Name, string Address, string PhoneNumber, decimal Latitude, decimal Longitude);
