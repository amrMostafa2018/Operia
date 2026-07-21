namespace Operia.Application.Common.Models;

public sealed class FileUploadContent(Stream content, string fileName, string contentType) : IAsyncDisposable
{
    public Stream Content { get; } = content;

    public string FileName { get; } = fileName;

    public string ContentType { get; } = contentType;

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
