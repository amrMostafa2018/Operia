using Operia.Application.Common.Models;

namespace Operia.Application.Employees;

public sealed record EmployeeDto(
    string Id, string Code, string FullName, string Email, string MobileNumber,
    string UserName, string? Specialty, string? JobTitle, DateOnly JoiningDate,
    string? PhotoUrl, bool IsActive, string Role, IReadOnlyList<EmployeeBranchDto> Branches,
    DateTime CreatedAt);

public sealed record EmployeeBranchDto(string Id, string Name);
public sealed record EmployeeRoleCountDto(string Role, int Count);
public sealed record EmployeeListResult(
    IReadOnlyList<EmployeeDto> Items, int PageNumber, int PageSize, int TotalCount,
    int TotalPages, IReadOnlyList<EmployeeRoleCountDto> RoleCounts);
public sealed record BookableEmployeeDto(string Id, string Code, string FullName, string? PhotoUrl, string? Specialty, string? JobTitle);

public sealed record EmployeeWriteModel(
    string FullName, string Email, string MobileNumber, string UserName,
    string? Specialty, string? JobTitle, DateOnly JoiningDate, bool IsActive,
    string Role, IReadOnlyList<string> BranchIds, string? TemporaryPassword,
    FileUploadContent? Photo, bool RemovePhoto);

public sealed record EmployeeListFilter(
    int PageNumber, int PageSize, string? Search, string? Role, bool? IsActive,
    string? BranchId, DateOnly? CreatedFrom, DateOnly? CreatedTo);

public interface IEmployeeService
{
    Task<EmployeeListResult> ListAsync(EmployeeListFilter filter, CancellationToken cancellationToken);
    Task<EmployeeDto> GetAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<BookableEmployeeDto>> BookableAsync(string branchId, CancellationToken cancellationToken);
    Task<EmployeeDto> CreateAsync(EmployeeWriteModel model, CancellationToken cancellationToken);
    Task<EmployeeDto> UpdateAsync(string id, EmployeeWriteModel model, CancellationToken cancellationToken);
    Task ChangeRoleAsync(string id, string role, CancellationToken cancellationToken);
    Task ChangeStatusAsync(string id, bool isActive, CancellationToken cancellationToken);
}
