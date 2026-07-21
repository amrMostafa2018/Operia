namespace Operia.Application.Onboarding.DTOs;

public sealed class SetupBusinessRequest
{
    public string BusinessName { get; set; } = string.Empty;

    public int BusinessType { get; set; }

    public string CountryCode { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;
}
