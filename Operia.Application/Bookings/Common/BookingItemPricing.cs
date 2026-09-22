using FluentValidation.Results;
using Operia.Application.Bookings.Commands.CreateBooking;
using Operia.Application.Common.Exceptions;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Common;

/// <summary>Shared booking line pricing for create and update operations.</summary>
public static class BookingItemPricing
{
    public static bool IsPackagePurchaseOnly(CreateBookingItemInput item) =>
        string.Equals(item.Type, "packagePurchase", StringComparison.OrdinalIgnoreCase);

    public static int PackageNewPurchaseUnits(
        CreateBookingItemInput item,
        IReadOnlySet<string> ownedCatalogPackageIds)
    {
        if (IsPackagePurchaseOnly(item))
        {
            return ownedCatalogPackageIds.Contains(item.PackageId)
                ? Math.Max(0, item.Quantity - 1)
                : item.Quantity;
        }

        return item.CustomerPackageId is not null
            ? Math.Max(0, item.Quantity - 1)
            : item.Quantity;
    }

    public static decimal CalculateItemTotal(
        CreateBookingItemInput item,
        Package product,
        IReadOnlySet<string> ownedCatalogPackageIds)
    {
        if (product.OfferType == OfferType.SingleSession)
        {
            return item.CustomerPackageId is not null ? 0 : product.Price * item.Quantity;
        }

        return product.Price * PackageNewPurchaseUnits(item, ownedCatalogPackageIds);
    }

    public static decimal CalculateUnitPrice(
        CreateBookingItemInput item,
        Package product,
        IReadOnlySet<string> ownedCatalogPackageIds)
    {
        if (product.OfferType == OfferType.SingleSession)
        {
            return item.CustomerPackageId is not null ? 0 : product.Price;
        }

        return PackageNewPurchaseUnits(item, ownedCatalogPackageIds) > 0 ? product.Price : 0;
    }

    /// <summary>Allows zero or one booking-session package plus optional purchase-only package lines.</summary>
    public static void ValidatePackageSelection(
        IReadOnlyList<CreateBookingItemInput> items,
        IReadOnlyDictionary<string, Package> catalog)
    {
        var sessionPackages = items
            .Where(item =>
                catalog[item.PackageId].OfferType == OfferType.Package &&
                !IsPackagePurchaseOnly(item))
            .ToList();
        if (sessionPackages.Count > 1)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.PackageEditNotAllowed, "items");
        }
    }

    /// <summary>Creates a customer-package row for a newly purchased package offer.</summary>
    public static CustomerPackage CreatePackagePurchase(
        Package product,
        string tenantId,
        string customerId,
        DateOnly today)
    {
        var total = product.PulseCount is > 0 ? product.PulseCount.Value : product.SessionCount ?? 0;
        if (total <= 0)
        {
            throw new ValidationException([new ValidationFailure("items", ApiErrorCodes.Packages.PackageSessionOrPulseRequired)
            {
                ErrorCode = ApiErrorCodes.Packages.PackageSessionOrPulseRequired
            }]);
        }

        DateOnly? expiresOn = product.PackageExpiryMonths is > 0
            ? today.AddMonths(product.PackageExpiryMonths.Value)
            : null;

        return new CustomerPackage
        {
            TenantId = tenantId,
            CustomerId = customerId,
            PackageId = product.Id,
            Total = total,
            ReservedSessions = 0,
            ExpiresOn = expiresOn,
            IsActive = true
        };
    }
}
