using QuestPDF.Drawing;

namespace Operia.Infrastructure.Export;

internal static class QuestPdfFontRegistrar
{
    private static readonly object Lock = new();
    private static bool _isRegistered;

    public static string ArabicFontFamily { get; private set; } = "Arial";

    public static void RegisterFonts()
    {
        lock (Lock)
        {
            if (_isRegistered)
            {
                return;
            }

            using var stream = typeof(QuestPdfFontRegistrar).Assembly
                .GetManifestResourceStream("Operia.Infrastructure.Fonts.NotoSansArabic-Regular.ttf");

            if (stream is not null)
            {
                FontManager.RegisterFont(stream);
                ArabicFontFamily = "Noto Sans Arabic";
            }

            _isRegistered = true;
        }
    }
}
