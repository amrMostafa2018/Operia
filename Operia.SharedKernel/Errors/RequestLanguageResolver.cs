namespace Operia.SharedKernel.Errors;

public static class RequestLanguageResolver
{
    public static string Resolve(string? acceptLanguageHeader)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguageHeader))
            return "en";

        var firstTag = acceptLanguageHeader
            .Split(',')[0]
            .Split(';')[0]
            .Trim();

        return firstTag.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";
    }
}
