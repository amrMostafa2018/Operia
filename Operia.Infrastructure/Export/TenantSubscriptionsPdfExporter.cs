using System.Globalization;
using Operia.Application.Common.Interfaces;
using Operia.Application.Finance.DTOs;
using Operia.Application.Finance.Resources;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace Operia.Infrastructure.Export;

public sealed class TenantSubscriptionsPdfExporter : ITenantSubscriptionsPdfExporter
{
    public byte[] Export(
        IReadOnlyList<TenantSubscriptionDto> subscriptions,
        FinanceResources financeResources)
    {
        QuestPdfFontRegistrar.RegisterFonts();

        var exportLabels = financeResources.SubscriptionsExport;
        var generatedAt = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        var fontFamily = QuestPdfFontRegistrar.ArabicFontFamily;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(style => style.FontFamily(fontFamily).FontSize(9));

                if (financeResources.IsArabic)
                {
                    page.ContentFromRightToLeft();
                }

                page.Header().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Text(exportLabels.SheetName).Bold().FontSize(16);
                    column.Item()
                        .Text($"{exportLabels.GeneratedAt}: {generatedAt}")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.2f);
                    });

                    table.Header(header =>
                    {
                        foreach (var label in exportLabels.Headers)
                        {
                            header.Cell()
                                .Background(Colors.Grey.Lighten3)
                                .Border(0.5f)
                                .BorderColor(Colors.Grey.Lighten1)
                                .Padding(6)
                                .Text(label)
                                .Bold();
                        }
                    });

                    foreach (var subscription in subscriptions)
                    {
                        AddCell(table, subscription.PlanName);
                        AddCell(table, subscription.PlanCode);
                        AddCell(table, financeResources.GetBillingTypeLabel(subscription.BillingType));
                        AddCell(table, subscription.Amount.ToString(CultureInfo.InvariantCulture));
                        AddCell(table, financeResources.GetCurrencyLabel(subscription.Currency));
                        AddCell(table, FormatDate(subscription.StartDate));
                        AddCell(table, FormatDate(subscription.EndDate));
                        AddCell(table, financeResources.GetStatusLabel(subscription.Status));
                    }
                });

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span($"{exportLabels.GeneratedAt}: ");
                        text.Span(generatedAt);
                    });
            });
        }).GeneratePdf();
    }

    private static void AddCell(TableDescriptor table, string value) =>
        table.Cell()
            .Border(0.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(6)
            .Text(value);

    private static string FormatDate(DateOnly? date) =>
        date?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
}
