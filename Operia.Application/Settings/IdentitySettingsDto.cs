namespace Operia.Application.Settings;

public sealed record IdentitySettingsDto(
    string ActivityName,
    string? ContactPhone,
    string? WhatsappPhone,
    string? Email,
    string? MainAddress,
    string? About,
    string? PrimaryPhotoUrl,
    IReadOnlyList<string> AdditionalPhotoUrls);
