using FluentAssertions;
using Operia.Application.Bookings.Common;
using Operia.Application.Bookings.Commands.CreateBooking;
using Operia.Application.Common.Exceptions;
using Operia.Application.Packages;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Xunit;

namespace Operia.Application.Tests.Packages;

public sealed class PackageCashPricingTests
{
    [Fact]
    public void CashPurchase_DiscountIsAppliedOnceAndSnapshotted()
    {
        var package = new Package
        {
            Price = 18000m,
            InstallmentPrice = 20000m,
            CashEnabled = true,
            InstallmentEnabled = true,
            DiscountCode = "SAVE10",
            DiscountPercent = 10m,
            OfferType = OfferType.Package,
            SessionCount = 6
        };
        var item = new CreateBookingItemInput(package.Id, null, 1);
        var owned = new HashSet<string>();

        PackageCashPricing.FinalPrice(package).Should().Be(16200m);
        BookingItemPricing.CalculateItemTotal(item, package, owned).Should().Be(16200m);
        BookingItemPricing.CalculateUnitPrice(item, package, owned).Should().Be(16200m);
        var purchase = BookingItemPricing.CreatePackagePurchase(package, "tenant-1", "customer-1", new DateOnly(2026, 10, 25));
        purchase.PurchasePrice.Should().Be(16200m);
        purchase.AmountDue.Should().Be(16200m);
        package.InstallmentPrice.Should().Be(20000m);
    }

    [Fact]
    public void CashDisabledOffer_CannotCreateCashPurchase()
    {
        var package = new Package
        {
            Price = 18000m,
            InstallmentPrice = 20000m,
            CashEnabled = false,
            InstallmentEnabled = true,
            OfferType = OfferType.SingleSession
        };

        var act = () => BookingItemPricing.CalculateItemTotal(
            new CreateBookingItemInput(package.Id, null, 1),
            package,
            new HashSet<string>());

        act.Should().Throw<ConflictException>();
    }
}
