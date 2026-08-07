namespace Operia.Contracts.Employees.CreateEmployee;

public sealed class CreateEmployeeForm
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
    public string TemporaryPassword { get; init; } = string.Empty;
    public IFormFile? Photo { get; init; }
}
