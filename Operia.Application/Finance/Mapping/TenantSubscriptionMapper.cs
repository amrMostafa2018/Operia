using Operia.Application.Finance.DTOs;
using Operia.Domain.Entities;
using Operia.Domain.Enums;

namespace Operia.Application.Finance.Mapping;

public static class TenantSubscriptionMapper
{
    public static TenantSubscriptionDto ToDto(TenantSubscription subscription) =>
        new(
            subscription.Id,
            subscription.Plan?.Code ?? string.Empty,
            subscription.Plan?.Name ?? string.Empty,
            MapBillingType(subscription.BillingType),
            subscription.Amount,
            subscription.Currency,
            subscription.StartDate,
            subscription.EndDate,
            MapStatus(subscription.Status));

    public static List<TenantSubscriptionDto> ToDtos(IEnumerable<TenantSubscription> subscriptions) =>
        subscriptions.Select(ToDto).ToList();

    private static string MapBillingType(BillingType billingType) =>
        billingType switch
        {
            BillingType.Monthly => "monthly",
            BillingType.Yearly => "yearly",
            _ => billingType.ToString().ToLowerInvariant()
        };

    private static string MapStatus(SubscriptionStatus status) =>
        status switch
        {
            SubscriptionStatus.Active => "active",
            SubscriptionStatus.Expired => "expired",
            SubscriptionStatus.Cancelled => "cancelled",
            SubscriptionStatus.Pending => "pending",
            _ => status.ToString().ToLowerInvariant()
        };
}
