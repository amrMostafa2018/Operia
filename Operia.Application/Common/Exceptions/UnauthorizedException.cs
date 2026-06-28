using Operia.SharedKernel.Errors;

namespace Operia.Application.Common.Exceptions;

public sealed class UnauthorizedException : Exception
{
    public string Field { get; }

    public string? ErrorCode { get; }

    public UnauthorizedException(string message, string field = "detail")
        : base(message)
    {
        Field = field;
    }

    public static UnauthorizedException FromCode(
        string errorCode,
        string field,
        string language = "en") =>
        new(errorCode, field, language);

    private UnauthorizedException(string errorCode, string field, string language)
        : base(ApiErrorCatalog.GetMessage(errorCode, language))
    {
        ErrorCode = errorCode;
        Field = field;
    }
}
