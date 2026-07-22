using MediatR;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Models;
using Operia.Application.Employees;
using Operia.Application.Employees.Commands.ChangeEmployeeRole;
using Operia.Application.Employees.Commands.ChangeEmployeeStatus;
using Operia.Application.Employees.Commands.CreateEmployee;
using Operia.Application.Employees.Commands.UpdateEmployee;
using Operia.Application.Employees.Queries.GetBookableEmployees;
using Operia.Application.Employees.Queries.GetEmployee;
using Operia.Application.Employees.Queries.ListEmployees;

namespace Operia.Controllers;

[ApiController]
[Route("api/employees")]
public sealed class EmployeesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Admin.EmployeesRead)]
    public Task<EmployeeListResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, [FromQuery] string? role = null, [FromQuery] bool? isActive = null,
        [FromQuery] string? branchId = null, [FromQuery] DateOnly? createdFrom = null, [FromQuery] DateOnly? createdTo = null,
        CancellationToken ct = default) => mediator.Send(new ListEmployeesQuery(pageNumber, pageSize, search, role, isActive, branchId, createdFrom, createdTo), ct);

    [HttpGet("{id}")]
    [HasPermission(Permissions.Admin.EmployeesRead)]
    public Task<EmployeeDto> Get(string id, CancellationToken ct) => mediator.Send(new GetEmployeeQuery(id), ct);

    [HttpGet("bookable")]
    [HasPermission(Permissions.Admin.BookingRead)]
    public Task<IReadOnlyList<BookableEmployeeDto>> Bookable([FromQuery] string branchId, CancellationToken ct) => mediator.Send(new GetBookableEmployeesQuery(branchId), ct);

    [HttpPost]
    [HasPermission(Permissions.Admin.EmployeesManage)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<EmployeeDto>> Create([FromForm] EmployeeCreateForm form, CancellationToken ct)
    {
        await using var photo = Open(form.Photo);
        var employee = await mediator.Send(new CreateEmployeeCommand(form.FullName, form.Email, form.MobileNumber, form.UserName,
            form.Specialty, form.JobTitle, form.JoiningDate, form.IsActive, form.Role, form.BranchIds,
            form.TemporaryPassword, photo), ct);
        return CreatedAtAction(nameof(Get), new { id = employee.Id }, employee);
    }

    [HttpPut("{id}")]
    [HasPermission(Permissions.Admin.EmployeesManage)]
    [Consumes("multipart/form-data")]
    public async Task<EmployeeDto> Update(string id, [FromForm] EmployeeUpdateForm form, CancellationToken ct)
    {
        await using var photo = Open(form.Photo);
        return await mediator.Send(new UpdateEmployeeCommand(id, form.FullName, form.Email, form.MobileNumber, form.UserName,
            form.Specialty, form.JobTitle, form.JoiningDate, form.IsActive, form.Role, form.BranchIds, photo, form.RemovePhoto), ct);
    }

    [HttpPatch("{id}/role")]
    [HasPermission(Permissions.Admin.EmployeesManage)]
    public async Task<IActionResult> Role(string id, ChangeRoleRequest request, CancellationToken ct)
    { await mediator.Send(new ChangeEmployeeRoleCommand(id, request.Role), ct); return NoContent(); }

    [HttpPatch("{id}/status")]
    [HasPermission(Permissions.Admin.EmployeesManage)]
    public async Task<IActionResult> Status(string id, ChangeStatusRequest request, CancellationToken ct)
    { await mediator.Send(new ChangeEmployeeStatusCommand(id, request.IsActive), ct); return NoContent(); }

    private static FileUploadContent? Open(IFormFile? file) => file is null ? null : new FileUploadContent(file.OpenReadStream(), file.FileName, file.ContentType);
}

public class EmployeeUpdateForm
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public string? JobTitle { get; set; }
    public DateOnly JoiningDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string Role { get; set; } = string.Empty;
    public List<string> BranchIds { get; set; } = [];
    public IFormFile? Photo { get; set; }
    public bool RemovePhoto { get; set; }
}
public sealed class EmployeeCreateForm : EmployeeUpdateForm { public string TemporaryPassword { get; set; } = string.Empty; }
public sealed record ChangeRoleRequest(string Role);
public sealed record ChangeStatusRequest(bool IsActive);
