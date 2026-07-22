namespace Operia.Application.Common.Models;

public sealed record FileExportResult(
    byte[] Content,
    string ContentType,
    string FileName);
