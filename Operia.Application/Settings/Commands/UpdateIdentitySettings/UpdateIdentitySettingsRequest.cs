namespace Operia.Application.Settings.Commands.UpdateIdentitySettings;

public sealed record SettingsImageUpload(
    Stream Content,
    string FileName,
    string ContentType,
    bool IsPrimary);

public sealed record UpdateIdentitySettingsRequest(
    string ActivityName,
    string? ContactPhone,
    string? WhatsappPhone,
    string? Email,
    string? MainAddress,
    string? About,
    IReadOnlyList<string> ExistingPhotoUrls,
    IReadOnlyList<SettingsImageUpload> NewPhotos);
