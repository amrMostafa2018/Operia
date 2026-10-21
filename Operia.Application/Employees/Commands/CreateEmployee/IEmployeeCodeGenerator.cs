namespace Operia.Application.Employees.Commands.CreateEmployee;

/// <summary>
/// Reserves the next employee code for a tenant as part of employee creation.
/// </summary>
public interface IEmployeeCodeGenerator
{
    Task<string> ReserveNextAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}
