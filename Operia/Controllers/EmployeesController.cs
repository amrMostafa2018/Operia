using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Operia.Application.Auth;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Common.Models;
using Operia.Application.Employees;
using Operia.Application.Employees.Commands.ChangeEmployeeRole;
using Operia.Application.Employees.Commands.ChangeEmployeeStatus;
using Operia.Application.Employees.Commands.CreateEmployee;
using Operia.Application.Employees.Commands.UpdateEmployee;
using Operia.Application.Employees.Commands.UpdateEmployeeSchedule;
using Operia.Application.Employees.Queries.GetBookableEmployees;
using Operia.Application.Employees.Queries.GetEmployee;
using Operia.Application.Employees.Queries.GetEmployeeSchedule;
using Operia.Application.Employees.Queries.ListEmployees;
using Operia.Contracts.Employees.ChangeEmployeeRole;
using Operia.Contracts.Employees.ChangeEmployeeStatus;
using Operia.Contracts.Employees.CreateEmployee;
using Operia.Contracts.Employees.UpdateEmployee;
using Operia.Contracts.Employees.UpdateEmployeeSchedule;

namespace Operia.Controllers;

[ApiController]
[Route("api/employees")]
public sealed class EmployeesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.EmployeesRead)]
    public Task<EmployeeListResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? branchId = null,
        [FromQuery] DateOnly? createdFrom = null,
        [FromQuery] DateOnly? createdTo = null,
        CancellationToken cancellationToken = default) =>
        mediator.Send(
            new ListEmployeesQuery(
                pageNumber,
                pageSize,
                search,
                role,
                isActive,
                branchId,
                createdFrom,
                createdTo),
            cancellationToken);

    [HttpGet("{id}")]
    [Authorize(Policy = Policies.EmployeesRead)]
    public Task<EmployeeDto> Get(string id, CancellationToken cancellationToken) =>
        mediator.Send(new GetEmployeeQuery(id), cancellationToken);

    [HttpGet("{id}/schedule")]
    [Authorize(Policy = Policies.EmployeesRead)]
    public Task<EmployeeScheduleDto> GetSchedule(string id, CancellationToken cancellationToken) =>
        mediator.Send(new GetEmployeeScheduleQuery(id), cancellationToken);

    [HttpPut("{id}/schedule")]
    [Authorize(Policy = Policies.EmployeesManage)]
    public Task<EmployeeScheduleDto> UpdateSchedule(
        string id,
        [FromBody] UpdateEmployeeScheduleRequest request,
        CancellationToken cancellationToken) =>
        mediator.Send(new UpdateEmployeeScheduleCommand(
            id,
            request.Branches
                .Select(branch => new EmployeeBranchScheduleInput(branch.BranchId, branch.Days))
                .ToList()),
            cancellationToken);

    [HttpGet("bookable")]
    [Authorize(Policy = Policies.BookingsManage)]
    public Task<IReadOnlyList<BookableEmployeeDto>> Bookable(
        [FromQuery] string branchId,
        CancellationToken cancellationToken) =>
        mediator.Send(new GetBookableEmployeesQuery(branchId), cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.EmployeesManage)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<EmployeeDto>> Create(
        [FromForm] CreateEmployeeForm form,
        CancellationToken cancellationToken)
    {
        await using var photo = Open(form.Photo);

        IReadOnlyList<EmployeeBranchScheduleInput>? schedule = null;
        if (!string.IsNullOrWhiteSpace(form.ScheduleJson))
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var branches = JsonSerializer.Deserialize<List<BranchScheduleRequest>>(form.ScheduleJson, options);
            schedule = branches?
                .Select(b => new EmployeeBranchScheduleInput(b.BranchId, b.Days))
                .ToList();
        }

        var command = new CreateEmployeeCommand(
            form.FullName,
            form.Email,
            form.MobileNumber,
            form.UserName,
            form.Specialty,
            form.JobTitle,
            form.JoiningDate,
            form.IsActive,
            form.Role,
            form.BranchIds,
            form.TemporaryPassword,
            photo,
            schedule);

        var employee = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = employee.Id }, employee);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Policies.EmployeesManage)]
    [Consumes("multipart/form-data")]
    public async Task<EmployeeDto> Update(
        string id,
        [FromForm] UpdateEmployeeForm form,
        CancellationToken cancellationToken)
    {
        await using var photo = Open(form.Photo);
        var command = new UpdateEmployeeCommand(
            id,
            form.FullName,
            form.Email,
            form.MobileNumber,
            form.UserName,
            form.Specialty,
            form.JobTitle,
            form.JoiningDate,
            form.IsActive,
            form.Role,
            form.BranchIds,
            photo,
            form.RemovePhoto);

        return await mediator.Send(command, cancellationToken);
    }

    [HttpPatch("{id}/role")]
    [Authorize(Policy = Policies.EmployeesManage)]
    public async Task<IActionResult> Role(
        string id,
        [FromBody] ChangeEmployeeRoleRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new ChangeEmployeeRoleCommand(id, request.Role),
            cancellationToken);

        return NoContent();
    }

    [HttpPatch("{id}/status")]
    [Authorize(Policy = Policies.EmployeesManage)]
    public async Task<IActionResult> Status(
        string id,
        [FromBody] ChangeEmployeeStatusRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new ChangeEmployeeStatusCommand(id, request.IsActive),
            cancellationToken);

        return NoContent();
    }

    private static FileUploadContent? Open(IFormFile? file) => file is null ? null : new FileUploadContent(file.OpenReadStream(), file.FileName, file.ContentType);
}
