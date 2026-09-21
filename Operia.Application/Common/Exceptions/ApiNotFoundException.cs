namespace Operia.Application.Common.Exceptions;

/// <summary>Represents a coded, client-visible missing-resource response.</summary>
public sealed class ApiNotFoundException : Exception
{
    public string Field { get; }
    public string ErrorCode { get; }

    private ApiNotFoundException(string errorCode, string field)
        : base(errorCode)
    {
        ErrorCode = errorCode;
        Field = field;
    }

    public static ApiNotFoundException FromCode(string errorCode, string field = "detail") =>
        new(errorCode, field);
}
