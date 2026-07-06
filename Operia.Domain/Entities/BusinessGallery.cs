namespace Operia.Domain.Entities;

public sealed class BusinessGallery
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BusinessId { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsMainImage { get; set; }
    public DateTime UploadedAt { get; set; }

    public Business? Business { get; set; }
}
