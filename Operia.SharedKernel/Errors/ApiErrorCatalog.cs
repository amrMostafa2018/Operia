using System.Reflection;
using System.Text.Json;

namespace Operia.SharedKernel.Errors;

public static class ApiErrorCatalog
{
    private static readonly IReadOnlyDictionary<string, string> EnglishMessages = Load("en");
    private static readonly IReadOnlyDictionary<string, string> ArabicMessages = Load("ar");

    public static string GetMessage(string code, string language = "en")
    {
        var messages = language.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
            ? ArabicMessages
            : EnglishMessages;

        return messages.TryGetValue(code, out var message) ? message : code;
    }

    public static IReadOnlyDictionary<string, string> GetAll(string language = "en") =>
        language.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
            ? ArabicMessages
            : EnglishMessages;

    private static IReadOnlyDictionary<string, string> Load(string language)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = ResolveResourceName(assembly, language);

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");

        var entries = JsonSerializer.Deserialize<List<ApiErrorEntry>>(stream)
            ?? throw new InvalidOperationException($"Failed to deserialize {resourceName}.");

        return entries.ToDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal);
    }

    private static string ResolveResourceName(Assembly assembly, string language)
    {
        var expected = $"Operia.SharedKernel.Errors.api-errors.{language}.json";
        var resourceNames = assembly.GetManifestResourceNames();

        return resourceNames.Contains(expected)
            ? expected
            : resourceNames.FirstOrDefault(name =>
                  name.EndsWith($".api-errors.{language}.json", StringComparison.OrdinalIgnoreCase))
              ?? expected;
    }
}
