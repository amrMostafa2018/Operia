using System.Text.Json;

namespace Operia.Application.Finance.Resources;

public static class FinanceResourceLocalizer
{
    private static readonly Lazy<IReadOnlyDictionary<string, FinanceResources>> ResourcesByLanguage =
        new(LoadAll);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static FinanceResources GetResources(string language)
    {
        if (language.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
            && ResourcesByLanguage.Value.TryGetValue("ar", out var arabicResources))
        {
            return arabicResources;
        }

        return ResourcesByLanguage.Value["en"];
    }

    private static IReadOnlyDictionary<string, FinanceResources> LoadAll()
    {
        return new Dictionary<string, FinanceResources>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = LoadFromResource("en"),
            ["ar"] = LoadFromResource("ar"),
        };
    }

    private static FinanceResources LoadFromResource(string language)
    {
        using var stream = OpenResourceStream(language);
        var resource = JsonSerializer.Deserialize<FinanceResourceRoot>(stream, JsonOptions)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize finance resources for '{language}'.");

        var finance = resource.Finance;
        var export = finance.Subscriptions.Export;
        var headers = export.Headers;

        return new FinanceResources(
            IsArabic: language.Equals("ar", StringComparison.OrdinalIgnoreCase),
            Currencies: finance.Currencies,
            Yearly: finance.Billing.Yearly,
            Monthly: finance.Billing.Monthly,
            Active: finance.Status.Active,
            Expired: finance.Status.Expired,
            Cancelled: finance.Status.Cancelled,
            Pending: finance.Status.Pending,
            SubscriptionsExport: new FinanceSubscriptionsExportLabels(
                SheetName: export.SheetName,
                GeneratedAt: export.GeneratedAt,
                Headers:
                [
                    headers.Plan,
                    headers.PlanCode,
                    headers.BillingType,
                    headers.Amount,
                    headers.Currency,
                    headers.StartDate,
                    headers.EndDate,
                    headers.Status
                ]));
    }

    private static Stream OpenResourceStream(string language)
    {
        var filePath = Path.Combine(
            AppContext.BaseDirectory,
            "Resources",
            $"{language}.json");

        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException(
                $"Finance resource file for '{language}' was not found at '{filePath}'.");
        }

        return File.OpenRead(filePath);
    }
}

public sealed record FinanceResources(
    bool IsArabic,
    IReadOnlyDictionary<string, string> Currencies,
    string Yearly,
    string Monthly,
    string Active,
    string Expired,
    string Cancelled,
    string Pending,
    FinanceSubscriptionsExportLabels SubscriptionsExport)
{
    public string GetBillingTypeLabel(string billingType) =>
        billingType switch
        {
            "yearly" => Yearly,
            "monthly" => Monthly,
            _ => billingType
        };

    public string GetStatusLabel(string status) =>
        status switch
        {
            "active" => Active,
            "expired" => Expired,
            "cancelled" => Cancelled,
            "pending" => Pending,
            _ => status
        };

    public string GetCurrencyLabel(string currency) =>
        Currencies.TryGetValue(currency, out var label) ? label : currency;
}

public sealed record FinanceSubscriptionsExportLabels(
    string SheetName,
    string GeneratedAt,
    string[] Headers);
