namespace Operia.Application.Packages.DTOs;

public sealed record PackageListResultDto(
    IReadOnlyList<PackageListItemDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    int ActiveCount,
    int CancelledCount);
