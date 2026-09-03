using Operia.Domain.Entities;
using Operia.Domain.Enums;

namespace Operia.Application.Tests.Helpers;

internal static class PackageTestData
{
    public const string DefaultBusinessId = "business-tenant-1";

    public static string BusinessIdForTenant(string tenantId) => $"business-{tenantId}";

    public static ServiceCategory ServiceCategory(
        string id,
        string tenantId,
        string name,
        string icon = "pi-tag",
        string? businessId = null) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            BusinessId = businessId ?? BusinessIdForTenant(tenantId),
            Name = name,
            Icon = icon,
            IsActive = true
        };

    public static SubServiceCategory SubServiceCategory(
        string id,
        string tenantId,
        string serviceCategoryId,
        string name,
        string? businessId = null) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            BusinessId = businessId ?? BusinessIdForTenant(tenantId),
            ServiceCategoryId = serviceCategoryId,
            Name = name,
            IsActive = true
        };

    public static Package Package(
        string id,
        string tenantId,
        string serviceCategoryId,
        string name,
        OfferType offerType = OfferType.SingleSession,
        PackageStatus status = PackageStatus.Active,
        string? subServiceCategoryId = null,
        string? businessId = null) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            BusinessId = businessId ?? BusinessIdForTenant(tenantId),
            Name = name,
            Description = "Description",
            OfferType = offerType,
            ServiceCategoryId = serviceCategoryId,
            SubServiceCategoryId = subServiceCategoryId,
            SessionDurationMinutes = 30,
            SessionCount = offerType == OfferType.Package ? 6 : 0,
            PulseCount = offerType == OfferType.Package ? 0 : null,
            PackageExpiryMonths = offerType == OfferType.Package ? 12 : null,
            Price = 1000,
            DiscountCode = string.Empty,
            DiscountPercent = 0,
            Status = status
        };
}
