namespace Operia.Infrastructure.Options;

public sealed class OtpSettings
{
    public const string SectionName = "OtpSettings";

    public int ExpiryMinutes { get; set; } = 5;
}
