namespace Operia.Domain.Entities;

public sealed class SubscriptionPlan
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public decimal YearlyPrice { get; set; }
    public string FeaturesJson { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int TrialDays { get; set; }

    public ICollection<TenantSubscription> Subscriptions { get; set; } = [];
}
