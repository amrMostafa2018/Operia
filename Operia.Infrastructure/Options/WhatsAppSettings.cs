namespace Operia.Infrastructure.Options;

public sealed class WhatsAppSettings
{
    public const string SectionName = "WhatsApp";

    public string ApiUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}
