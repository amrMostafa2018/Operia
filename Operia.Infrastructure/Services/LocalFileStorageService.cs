using FluentValidation.Results;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Infrastructure.Options;

namespace Operia.Infrastructure.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly FileStorageSettings _settings;

    public LocalFileStorageService(
        IWebHostEnvironment environment,
        IOptions<FileStorageSettings> settings)
    {
        _environment = environment;
        _settings = settings.Value;
    }

    public async Task<string> SaveAsync(
        Stream content,
        string originalFileName,
        string contentType,
        string tenantId,
        FileUploadCategory category,
        CancellationToken cancellationToken = default)
    {
        ValidateUpload(contentType, content.Length);

        var folder = category switch
        {
            FileUploadCategory.BusinessGallery => _settings.BusinessGalleriesFolder,
            FileUploadCategory.PlatformRevenue => _settings.PlatformRevenuesFolder,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
        };

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => string.Empty
            };
        }

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var webRootPath = GetWebRootPath();
        var relativeUrl = $"/uploads/{tenantId}/{folder}/{fileName}";
        var physicalDirectory = Path.Combine(
            webRootPath,
            "uploads",
            tenantId,
            folder);
        var physicalPath = Path.Combine(physicalDirectory, fileName);

        Directory.CreateDirectory(physicalDirectory);

        await using var fileStream = new FileStream(
            physicalPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);
        await content.CopyToAsync(fileStream, cancellationToken);

        return relativeUrl;
    }

    public Task DeleteAsync(string relativeUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl) || !relativeUrl.StartsWith("/uploads/", StringComparison.Ordinal))
            return Task.CompletedTask;

        var relativePath = relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.Combine(GetWebRootPath(), relativePath);

        if (File.Exists(physicalPath))
            File.Delete(physicalPath);

        return Task.CompletedTask;
    }

    private string GetWebRootPath()
    {
        var webRootPath = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;

        Directory.CreateDirectory(webRootPath);
        return webRootPath;
    }

    private void ValidateUpload(string contentType, long contentLength)
    {
        if (contentLength <= 0)
        {
            throw new ValidationException(
            [
                new ValidationFailure("file", "Uploaded file is empty.")
            ]);
        }

        if (contentLength > _settings.MaxFileSizeBytes)
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    "file",
                    $"Uploaded file exceeds the maximum size of {_settings.MaxFileSizeBytes} bytes.")
            ]);
        }

        if (!_settings.AllowedMimeTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException(
            [
                new ValidationFailure("file", "Uploaded file type is not allowed.")
            ]);
        }
    }
}
