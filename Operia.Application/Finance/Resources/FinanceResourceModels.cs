using System.Text.Json.Serialization;

namespace Operia.Application.Finance.Resources;

internal sealed class FinanceResourceRoot
{
    [JsonPropertyName("FINANCE")]
    public FinanceResource Finance { get; set; } = new();
}

internal sealed class FinanceResource
{
    [JsonPropertyName("CURRENCIES")]
    public Dictionary<string, string> Currencies { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("BILLING")]
    public FinanceBillingResource Billing { get; set; } = new();

    [JsonPropertyName("STATUS")]
    public FinanceStatusResource Status { get; set; } = new();

    [JsonPropertyName("SUBSCRIPTIONS")]
    public FinanceSubscriptionsResource Subscriptions { get; set; } = new();
}

internal sealed class FinanceSubscriptionsResource
{
    [JsonPropertyName("EXPORT")]
    public FinanceSubscriptionsExportResource Export { get; set; } = new();
}

internal sealed class FinanceSubscriptionsExportResource
{
    [JsonPropertyName("SHEET_NAME")]
    public string SheetName { get; set; } = string.Empty;

    [JsonPropertyName("GENERATED_AT")]
    public string GeneratedAt { get; set; } = string.Empty;

    [JsonPropertyName("HEADERS")]
    public FinanceSubscriptionsExportHeadersResource Headers { get; set; } = new();
}

internal sealed class FinanceSubscriptionsExportHeadersResource
{
    [JsonPropertyName("PLAN")]
    public string Plan { get; set; } = string.Empty;

    [JsonPropertyName("PLAN_CODE")]
    public string PlanCode { get; set; } = string.Empty;

    [JsonPropertyName("BILLING_TYPE")]
    public string BillingType { get; set; } = string.Empty;

    [JsonPropertyName("AMOUNT")]
    public string Amount { get; set; } = string.Empty;

    [JsonPropertyName("CURRENCY")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("START_DATE")]
    public string StartDate { get; set; } = string.Empty;

    [JsonPropertyName("END_DATE")]
    public string EndDate { get; set; } = string.Empty;

    [JsonPropertyName("STATUS")]
    public string Status { get; set; } = string.Empty;
}

internal sealed class FinanceBillingResource
{
    [JsonPropertyName("YEARLY")]
    public string Yearly { get; set; } = string.Empty;

    [JsonPropertyName("MONTHLY")]
    public string Monthly { get; set; } = string.Empty;
}

internal sealed class FinanceStatusResource
{
    [JsonPropertyName("ACTIVE")]
    public string Active { get; set; } = string.Empty;

    [JsonPropertyName("EXPIRED")]
    public string Expired { get; set; } = string.Empty;

    [JsonPropertyName("CANCELLED")]
    public string Cancelled { get; set; } = string.Empty;

    [JsonPropertyName("PENDING")]
    public string Pending { get; set; } = string.Empty;
}
