using Operia.SharedKernel.Errors;

namespace Operia.Application.Common.Exceptions;

public sealed class ForbiddenException : Exception
{
    public string Field { get; }

    public string? ErrorCode { get; }

    public ForbiddenException(string message, string field = "detail")
        : base(message)
    {
        Field = field;
    }

    public static ForbiddenException FromCode(string errorCode, string field = "detail") =>
        new(errorCode, field, isErrorCode: true);

    private ForbiddenException(string errorCode, string field, bool isErrorCode)
        : base(errorCode)
    {
        ErrorCode = errorCode;
        Field = field;
    }
}
