namespace Operia.Contracts.Employees.UpdateEmployee;

public sealed class UpdateEmployeeForm
{
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string MobileNumber { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string? Specialty { get; init; }
    public string? JobTitle { get; init; }
    public DateOnly JoiningDate { get; init; }
    public bool IsActive { get; init; } = true;
    public string Role { get; init; } = string.Empty;
    public List<string> BranchIds { get; init; } = [];
    public IFormFile? Photo { get; init; }
    public bool RemovePhoto { get; init; }
}
