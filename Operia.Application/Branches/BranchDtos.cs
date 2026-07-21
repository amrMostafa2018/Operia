using Operia.Application.Common.Exceptions;

namespace Operia.Application.Branches;

public sealed record BranchDto(
    string Id,
    string Name,
    string Address,
    string PhoneNumber,
    decimal Latitude,
    decimal Longitude,
    string GoogleMapsUrl);

public sealed record BranchListResult(
    IReadOnlyList<BranchDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    int TotalTenantCount);

public interface IBranchDependencyProbe
{
    Task<BranchDependencyInfo?> GetDependencyAsync(string tenantId, string branchId, CancellationToken cancellationToken);
}
