using Operia.Domain.Common;
using Operia.Domain.Enums;
using Operia.Domain.Interfaces;

namespace Operia.Domain.Entities;

public sealed class Package : Entity, ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string BusinessId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public OfferType OfferType { get; set; } = OfferType.SingleSession;
    public string ServiceCategoryId { get; set; } = string.Empty;
    public string? SubServiceCategoryId { get; set; }
    public int SessionDurationMinutes { get; set; }
    public int SessionCount { get; set; }
    public int? PulseCount { get; set; }
    public int? PackageExpiryMonths { get; set; }
    public decimal Price { get; set; }
    public string DiscountCode { get; set; } = string.Empty;
    public decimal? DiscountPercent { get; set; }
    public PackageStatus Status { get; set; } = PackageStatus.Active;
    public DateOnly? EndsAt { get; set; }

    public Business? Business { get; set; }
    public ServiceCategory? ServiceCategory { get; set; }
    public SubServiceCategory? SubServiceCategory { get; set; }
}
