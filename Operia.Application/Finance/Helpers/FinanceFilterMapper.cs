using Operia.Domain.Enums;

namespace Operia.Application.Finance.Helpers;

public static class FinanceFilterMapper
{
    public static SubscriptionStatus? ParseStatus(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "active" => SubscriptionStatus.Active,
            "expired" => SubscriptionStatus.Expired,
            "cancelled" => SubscriptionStatus.Cancelled,
            "pending" => SubscriptionStatus.Pending,
            _ => Enum.TryParse<SubscriptionStatus>(value, true, out var parsed)
                ? parsed
                : null
        };
}
