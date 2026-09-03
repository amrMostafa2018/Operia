using Operia.Application.Common.Interfaces;
using Operia.Application.Common.Exceptions;
using Operia.Application.Packages.DTOs;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Packages;

internal static class PackageMapper
{
    public static string RequireTenant(ICurrentUserService currentUser) =>
        currentUser.TenantId
        ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);

    public static string ToOfferTypeJson(OfferType offerType) =>
        offerType switch
        {
            OfferType.Package => "package",
            _ => "singleSession"
        };

    public static OfferType ParseOfferType(string? value) =>
        string.Equals(value, "package", StringComparison.OrdinalIgnoreCase)
            ? OfferType.Package
            : OfferType.SingleSession;

    public static string ToStatusJson(PackageStatus status) =>
        status switch
        {
            PackageStatus.Cancelled => "cancelled",
            _ => "active"
        };

    public static PackageStatus ParseStatus(string? value) =>
        string.Equals(value, "cancelled", StringComparison.OrdinalIgnoreCase)
            ? PackageStatus.Cancelled
            : PackageStatus.Active;

    public static PackageStatus FromIsActive(bool isActive) =>
        isActive ? PackageStatus.Active : PackageStatus.Cancelled;

    public static ServiceCategoryDto ToDto(ServiceCategory category) =>
        new(category.Id, category.Name, category.Icon);

    public static SubServiceCategoryDto ToDto(SubServiceCategory category) =>
        new(category.Id, category.Name, category.ServiceCategoryId);

    public static PackageListItemDto ToListItemDto(Package package) =>
        new(
            package.Id,
            package.Name,
            ToOfferTypeJson(package.OfferType),
            package.SessionDurationMinutes,
            package.ServiceCategoryId,
            package.ServiceCategory?.Name ?? string.Empty,
            package.Price,
            ToStatusJson(package.Status),
            package.CreatedAt,
            package.EndsAt?.ToString("yyyy-MM-dd"));

    public static PackageDetailDto ToDetailDto(Package package) =>
        new(
            package.Id,
            package.Name,
            ToOfferTypeJson(package.OfferType),
            package.SessionDurationMinutes,
            package.ServiceCategoryId,
            package.ServiceCategory?.Name ?? string.Empty,
            package.Price,
            ToStatusJson(package.Status),
            package.CreatedAt,
            package.EndsAt?.ToString("yyyy-MM-dd"),
            package.Description,
            package.SubServiceCategoryId,
            package.SessionCount,
            package.PulseCount,
            package.PackageExpiryMonths,
            package.DiscountCode,
            package.DiscountPercent);
}
