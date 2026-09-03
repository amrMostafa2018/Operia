namespace Operia.Application.Packages.DTOs;

public sealed record PackageDetailDto(
    string Id,
    string Name,
    string OfferType,
    int SessionDurationMinutes,
    string ServiceCategoryId,
    string ServiceCategoryName,
    decimal Price,
    string Status,
    DateTime CreatedAt,
    string? EndsAt,
    string Description,
    string? SubServiceCategoryId,
    int SessionCount,
    int? PulseCount,
    int? PackageExpiryMonths,
    string DiscountCode,
    decimal? DiscountPercent);
