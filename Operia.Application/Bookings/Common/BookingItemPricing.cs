using FluentValidation.Results;
using Operia.Application.Bookings.Commands.CreateBooking;
using Operia.Application.Common.Exceptions;
using Operia.Application.Packages;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Common;

/// <summary>Shared booking line pricing for create and update operations.</summary>
public static class BookingItemPricing
{
    public static bool IsPackagePurchaseOnly(CreateBookingItemInput item) =>
        string.Equals(item.Type, "packagePurchase", StringComparison.OrdinalIgnoreCase);

    public static bool IsInstallmentPurchase(CreateBookingItemInput item) =>
        string.Equals(item.PaymentOption, "installment", StringComparison.OrdinalIgnoreCase);

    public static int PackageNewPurchaseUnits(
        CreateBookingItemInput item,
        IReadOnlySet<string> ownedCatalogPackageIds)
    {
        if (IsPackagePurchaseOnly(item))
        {
            if (IsInstallmentPurchase(item))
            {
                return item.Quantity;
            }
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
            return item.CustomerPackageId is not null ? 0 : CashPrice(product) * item.Quantity;
        }

        var units = PackageNewPurchaseUnits(item, ownedCatalogPackageIds);
        return units > 0 ? SelectedPrice(item, product) * units : 0;
    }

    public static decimal CalculateUnitPrice(
        CreateBookingItemInput item,
        Package product,
        IReadOnlySet<string> ownedCatalogPackageIds)
    {
        if (product.OfferType == OfferType.SingleSession)
        {
            return item.CustomerPackageId is not null ? 0 : CashPrice(product);
        }

        return PackageNewPurchaseUnits(item, ownedCatalogPackageIds) > 0 ? SelectedPrice(item, product) : 0;
    }

    private static decimal SelectedPrice(CreateBookingItemInput item, Package product)
    {
        if (!IsInstallmentPurchase(item))
        {
            return CashPrice(product);
        }
        if (!product.InstallmentEnabled || product.InstallmentPrice is not > 0)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Packages.PackageInstallmentUnavailable, "items");
        }
        return product.InstallmentPrice.Value;
    }

    public static decimal CashPrice(Package product)
    {
        if (!product.CashEnabled)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Packages.PackageCashUnavailable, "items");
        }

        return PackageCashPricing.FinalPrice(product);
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
        foreach (var item in items)
        {
            if (item.PaymentOption is not null &&
                !string.Equals(item.PaymentOption, "cash", StringComparison.OrdinalIgnoreCase) &&
                !IsInstallmentPurchase(item))
            {
                throw ConflictException.FromCode(ApiErrorCodes.Packages.PackageInstallmentUnavailable, "items");
            }
            if (IsInstallmentPurchase(item) &&
                (catalog[item.PackageId].OfferType != OfferType.Package ||
                 !catalog[item.PackageId].InstallmentEnabled ||
                 !IsPackagePurchaseOnly(item) ||
                 item.CustomerPackageId is not null))
            {
                throw ConflictException.FromCode(ApiErrorCodes.Bookings.InstallmentPurchaseOnly, "items");
            }
        }
    }

    /// <summary>Creates a customer-package row for a newly purchased package offer.</summary>
    public static CustomerPackage CreatePackagePurchase(
        Package product,
        string tenantId,
        string customerId,
        DateOnly today,
        bool installment = false,
        string? originBookingId = null)
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

        var price = installment
            ? product.InstallmentPrice ?? throw ConflictException.FromCode(
                ApiErrorCodes.Packages.PackageInstallmentUnavailable, "items")
            : CashPrice(product);
        var purchase = new CustomerPackage
        {
            TenantId = tenantId,
            CustomerId = customerId,
            PackageId = product.Id,
            OriginBookingId = originBookingId,
            PurchasePrice = price,
            AmountDue = price,
            Total = installment ? 0 : total,
            ReservedSessions = 0,
            ExpiresOn = expiresOn,
            IsActive = true
        };
        if (installment)
        {
            if (product.SessionCount is not > 0 || product.PulseCount is > 0 ||
                product.FirstPayment is null || product.Installments.Count != product.InstallmentCount)
            {
                throw ConflictException.FromCode(ApiErrorCodes.Packages.PackageInstallmentUnavailable, "items");
            }
            purchase.Installments.Add(new CustomerPackageInstallment
            {
                TenantId = tenantId,
                CustomerPackageId = purchase.Id,
                Number = 0,
                Amount = product.FirstPayment.Value
            });
            foreach (var row in product.Installments.OrderBy(x => x.Number))
            {
                purchase.Installments.Add(new CustomerPackageInstallment
                {
                    TenantId = tenantId,
                    CustomerPackageId = purchase.Id,
                    Number = row.Number,
                    Amount = row.Amount,
                    CumulativeAvailableSessions = row.CumulativeAvailableSessions
                });
            }
        }
        return purchase;
    }
}
