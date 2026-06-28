using FluentValidation.Results;

namespace Operia.SharedKernel.Errors;

public static class ValidationFailureFactory
{
    public static ValidationFailure Create(string field, string errorCode, string language = "en") =>
        new(NormalizeFieldName(field), ApiErrorCatalog.GetMessage(errorCode, language))
        {
            ErrorCode = errorCode
        };

    public static ValidationFailure Create(string field, string errorCode, string message, string? language = null) =>
        new(NormalizeFieldName(field), message)
        {
            ErrorCode = errorCode
        };

    public static string NormalizeFieldName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
