namespace Operia.Infrastructure.Options;

public sealed class CorsSettings
{
    public const string SectionName = "Cors";
    public const string PolicyName = "OperiaAngular";

    public string[] AllowedOrigins { get; set; } = [];
}
