using Operia.Domain.Entities;

namespace Operia.Application.Packages;

public static class PackageCashPricing
{
    public static decimal FinalPrice(Package package)
    {
        var percent = package.DiscountPercent ?? 0;
        return decimal.Round(package.Price * (100 - percent) / 100, 2, MidpointRounding.AwayFromZero);
    }
}
