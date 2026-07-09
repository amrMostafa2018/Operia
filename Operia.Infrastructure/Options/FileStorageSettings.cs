namespace Operia.Infrastructure.Options;

public sealed class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    public string[] AllowedMimeTypes { get; set; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    ];

    public string BusinessGalleriesFolder { get; set; } = "BusinessGalleries";

    public string PlatformRevenuesFolder { get; set; } = "PlatformRevenues";
}
