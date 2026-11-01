using FluentAssertions;
using Operia.Application.Packages;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Xunit;

namespace Operia.Application.Tests.Packages;

public sealed class PackagePurchaseSnapshotTests
{
    [Fact]
    public void InstallmentTermsRemainUnchangedAfterOfferEdit()
    {
        var offer = new Package
        {
            Name = "Laser Package",
            OfferType = OfferType.Package,
            SessionDurationMinutes = 45,
            SessionCount = 10,
            Price = 18000m,
            InstallmentPrice = 20000m,
            FirstPayment = 5000m,
            ServiceCategoryId = "category-1",
            Installments =
            [
                new PackageInstallment
                {
                    Number = 1,
                    Amount = 7000m,
                    SessionsUnlocked = 4,
                    CumulativeAvailableSessions = 4
                },
                new PackageInstallment
                {
                    Number = 2,
                    Amount = 8000m,
                    SessionsUnlocked = 6,
                    CumulativeAvailableSessions = 10
                }
            ]
        };

        var storedJson = PackagePurchaseSnapshot.Capture(offer, "installment", 20000m).ToJson();
        offer.Name = "Changed Package";
        offer.InstallmentPrice = 24000m;
        offer.FirstPayment = 9000m;
        offer.Installments.First().Amount = 15000m;

        var purchased = PackagePurchaseSnapshot.FromJson(storedJson);
        purchased.Should().NotBeNull();
        purchased!.OfferName.Should().Be("Laser Package");
        purchased.PurchasePrice.Should().Be(20000m);
        purchased.FirstPayment.Should().Be(5000m);
        purchased.Installments.Select(row => row.Amount).Should().Equal(7000m, 8000m);
    }
}
