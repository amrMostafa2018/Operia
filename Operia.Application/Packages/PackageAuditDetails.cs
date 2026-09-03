using Operia.Domain.Entities;
using Operia.Domain.Enums;

namespace Operia.Application.Packages;

internal static class PackageAuditDetails
{
    public static object ForServiceCategory(ServiceCategory category) =>
        new
        {
            category.Name,
            category.Icon,
            category.BusinessId
        };

    public static object ForSubServiceCategory(SubServiceCategory category) =>
        new
        {
            category.Name,
            category.ServiceCategoryId,
            category.BusinessId
        };

    public static object ForPackage(Package package) =>
        new
        {
            package.Name,
            offerType = PackageMapper.ToOfferTypeJson(package.OfferType),
            status = PackageMapper.ToStatusJson(package.Status),
            package.Description,
            package.ServiceCategoryId,
            package.SubServiceCategoryId,
            package.SessionDurationMinutes,
            package.SessionCount,
            package.PulseCount,
            package.PackageExpiryMonths,
            package.Price,
            package.DiscountCode,
            package.DiscountPercent
        };

    public static object ForPackageUpdated(object previous, Package current) =>
        new
        {
            previous,
            current = ForPackage(current)
        };

    public static object ForPackageDeleted() =>
        new
        {
            previousStatus = PackageMapper.ToStatusJson(PackageStatus.Active),
            status = PackageMapper.ToStatusJson(PackageStatus.Cancelled)
        };
}
