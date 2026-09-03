namespace Operia.Application.Packages.DTOs;

public sealed record PackageListItemDto(
    string Id,
    string Name,
    string OfferType,
    int SessionDurationMinutes,
    string ServiceCategoryId,
    string ServiceCategoryName,
    decimal Price,
    string Status,
    DateTime CreatedAt,
    string? EndsAt);
