using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages;
using Operia.Application.Bookings.Common;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Sales.Queries.QuoteSale;

/// <summary>Prices a draft from current tenant data. No sale or payment is recorded.</summary>
public sealed class QuoteSaleHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope) : IRequestHandler<QuoteSaleQuery, SaleQuoteDto>
{
    public async Task<SaleQuoteDto> Handle(QuoteSaleQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);

        if (!string.IsNullOrWhiteSpace(request.BranchId))
        {
            var branchExists = await db.Branches.AsNoTracking()
                .AnyAsync(branch => branch.TenantId == tenantId && branch.Id == request.BranchId,
                    cancellationToken);
            var allowed = await branchScope.GetAllowedBranchIdsAsync(currentUser.UserId ?? string.Empty,
                cancellationToken);
            if (!branchExists || !allowed.Contains(request.BranchId))
            {
                throw ForbiddenException.FromCode(ApiErrorCodes.Sales.BranchUnavailable, "branchId");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerId) &&
            !await db.Customers.AsNoTracking().AnyAsync(customer =>
                customer.TenantId == tenantId && customer.Id == request.CustomerId && customer.IsActive,
                cancellationToken))
        {
            throw ApiNotFoundException.FromCode(ApiErrorCodes.Sales.CustomerUnavailable, "customerId");
        }

        var offerIds = request.Items.Where(item => !string.IsNullOrWhiteSpace(item.OfferId))
            .Select(item => item.OfferId!).Distinct().ToArray();
        var offers = await db.Packages.AsNoTracking().Include(offer => offer.Installments)
            .Where(offer => offer.TenantId == tenantId && offerIds.Contains(offer.Id))
            .ToDictionaryAsync(offer => offer.Id, cancellationToken);

        var ownedIds = request.Items.Where(item => !string.IsNullOrWhiteSpace(item.OwnedCustomerPackageId))
            .Select(item => item.OwnedCustomerPackageId!).Distinct().ToArray();
        var owned = await db.CustomerPackages.AsNoTracking()
            .Where(purchase => purchase.TenantId == tenantId && ownedIds.Contains(purchase.Id))
            .ToDictionaryAsync(purchase => purchase.Id, cancellationToken);

        var lines = new List<SaleQuoteLineDto>(request.Items.Count);
        var usedOwnedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in request.Items)
        {
            if (!string.IsNullOrWhiteSpace(item.OwnedCustomerPackageId))
            {
                if (!string.IsNullOrWhiteSpace(item.OfferId) || item.Quantity != 1 ||
                    request.CustomerId is null ||
                    !owned.TryGetValue(item.OwnedCustomerPackageId, out var purchase) ||
                    purchase.CustomerId != request.CustomerId || !purchase.IsActive ||
                    purchase.ExpiresOn < DateOnly.FromDateTime(DateTime.UtcNow) ||
                    !usedOwnedIds.Add(purchase.Id) || item.PaymentOption != "owned")
                {
                    throw ConflictException.FromCode(ApiErrorCodes.Sales.OwnedSessionUnavailable, "items");
                }

                if (!offers.TryGetValue(purchase.PackageId, out var ownedOffer))
                {
                    ownedOffer = await db.Packages.AsNoTracking().FirstOrDefaultAsync(offer =>
                        offer.TenantId == tenantId && offer.Id == purchase.PackageId,
                        cancellationToken);
                }
                var snapshot = PackagePurchaseSnapshot.FromJson(purchase.PurchaseSnapshotJson);
                if (ownedOffer is null || (snapshot?.PulseCount ?? ownedOffer.PulseCount) is > 0 ||
                    !CustomerPackageBalance.HasAvailable(
                        purchase.Total, purchase.Used, purchase.ReservedSessions,
                        snapshot?.OfferType ?? ownedOffer.OfferType,
                        snapshot?.PulseCount ?? ownedOffer.PulseCount,
                        PurchaseSessionEntitlement.Released(purchase)))
                {
                    throw ConflictException.FromCode(ApiErrorCodes.Sales.OwnedSessionUnavailable, "items");
                }
                lines.Add(new SaleQuoteLineDto(ownedOffer.Id, purchase.Id,
                    snapshot?.OfferName ?? ownedOffer.Name,
                    "owned", snapshot?.SessionDurationMinutes ?? ownedOffer.SessionDurationMinutes,
                    1, 1, "owned", 0, 0, 0, 0, null));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.OfferId))
            {
                if (!offers.TryGetValue(item.OfferId, out var offer) ||
                    offer.Status != PackageStatus.Active ||
                    offer.EndsAt is { } endsAt && endsAt < DateOnly.FromDateTime(DateTime.UtcNow))
                {
                    throw ConflictException.FromCode(ApiErrorCodes.Sales.OfferUnavailable, "items");
                }
                lines.Add(PriceOffer(offer, item));
                continue;
            }

            if (item.PaymentOption != "cash" || item.UnlistedName is null ||
                item.UnlistedPrice is null || item.UnlistedDurationMinutes is null)
            {
                throw ConflictException.FromCode(ApiErrorCodes.Sales.PaymentOptionInvalid, "items");
            }

            var name = item.UnlistedName.Trim();
            if (await db.Packages.AsNoTracking().AnyAsync(offer =>
                    offer.TenantId == tenantId && offer.Name.ToLower() == name.ToLower(),
                    cancellationToken))
            {
                throw ConflictException.FromCode(ApiErrorCodes.Sales.UnlistedMatchesCatalogOffer, "items");
            }
            var amount = item.UnlistedPrice.Value * item.Quantity;
            lines.Add(new SaleQuoteLineDto(null, null, name, "unlisted",
                item.UnlistedDurationMinutes.Value, null, item.Quantity, "cash",
                amount, 0, amount, amount, null));
        }

        return new SaleQuoteDto(lines,
            lines.Sum(line => line.ListPrice),
            lines.Sum(line => line.DiscountAmount),
            lines.Sum(line => line.FinalAmount),
            lines.Sum(line => line.DueNow));
    }

    private static SaleQuoteLineDto PriceOffer(Package offer, QuoteSaleItem item)
    {
        decimal listPrice;
        decimal finalAmount;
        decimal dueNow;
        if (item.PaymentOption == "cash" && offer.CashEnabled && offer.Price > 0 &&
            offer.DiscountPercent is null or >= 0 and <= 100)
        {
            listPrice = offer.Price * item.Quantity;
            finalAmount = PackageCashPricing.FinalPrice(offer) * item.Quantity;
            dueNow = finalAmount;
        }
        else if (item.PaymentOption == "installment" && offer.OfferType == OfferType.Package &&
                 offer.InstallmentEnabled && offer.InstallmentPrice is > 0 &&
                 offer.FirstPayment is >= 0 && offer.FirstPayment <= offer.InstallmentPrice &&
                 offer.InstallmentCount is > 0 &&
                 offer.Installments.Count == offer.InstallmentCount &&
                 offer.Installments.Sum(row => row.Amount) == offer.InstallmentPrice - offer.FirstPayment &&
                 offer.Installments.OrderBy(row => row.Number)
                     .Select((row, index) => row.Number == index + 1 && row.Amount >= 0)
                     .All(valid => valid))
        {
            listPrice = offer.InstallmentPrice.Value * item.Quantity;
            finalAmount = listPrice;
            dueNow = offer.FirstPayment.Value * item.Quantity;
        }
        else
        {
            throw ConflictException.FromCode(ApiErrorCodes.Sales.PaymentOptionUnavailable, "items");
        }

        return new SaleQuoteLineDto(offer.Id, null, offer.Name,
            offer.OfferType == OfferType.Package ? "package" : "singleSession",
            offer.SessionDurationMinutes, offer.SessionCount, item.Quantity,
            item.PaymentOption, listPrice, listPrice - finalAmount, finalAmount, dueNow,
            PackagePurchaseSnapshot.Capture(offer, item.PaymentOption, finalAmount / item.Quantity));
    }
}
