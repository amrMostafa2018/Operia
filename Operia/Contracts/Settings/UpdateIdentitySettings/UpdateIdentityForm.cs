namespace Operia.Contracts.Settings.UpdateIdentitySettings;

public sealed class UpdateIdentityForm
{
    public string ActivityName { get; init; } = string.Empty;
    public string? ContactPhone { get; init; }
    public string? WhatsappPhone { get; init; }
    public string? Email { get; init; }
    public string? MainAddress { get; init; }
    public string? About { get; init; }
    public List<string>? ExistingPhotoUrls { get; init; }
    public IFormFile? PrimaryPhoto { get; init; }
    public List<IFormFile>? AdditionalPhotos { get; init; }
}
