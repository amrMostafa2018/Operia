namespace Operia.Application.Common.Interfaces;

public enum FileUploadCategory
{
    BusinessGallery,
    PlatformRevenue
}

public interface IFileStorageService
{
    Task<string> SaveAsync(
        Stream content,
        string originalFileName,
        string contentType,
        string tenantId,
        FileUploadCategory category,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string relativeUrl, CancellationToken cancellationToken = default);
}
