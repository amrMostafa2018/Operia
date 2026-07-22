using System.Globalization;
using ClosedXML.Excel;
using Operia.Application.Common.Interfaces;
using Operia.Application.Finance.DTOs;
using Operia.Application.Finance.Resources;

namespace Operia.Infrastructure.Export;

public sealed class TenantSubscriptionsExcelExporter : ITenantSubscriptionsExcelExporter
{
    public byte[] Export(
        IReadOnlyList<TenantSubscriptionDto> subscriptions,
        FinanceResources financeResources)
    {
        var exportLabels = financeResources.SubscriptionsExport;

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(exportLabels.SheetName);
        worksheet.RightToLeft = financeResources.IsArabic;

        for (var column = 0; column < exportLabels.Headers.Length; column++)
        {
            var cell = worksheet.Cell(1, column + 1);
            cell.Value = exportLabels.Headers[column];
            cell.Style.Font.Bold = true;
        }

        var rowIndex = 2;
        foreach (var subscription in subscriptions)
        {
            worksheet.Cell(rowIndex, 1).Value = subscription.PlanName;
            worksheet.Cell(rowIndex, 2).Value = subscription.PlanCode;
            worksheet.Cell(rowIndex, 3).Value = financeResources.GetBillingTypeLabel(subscription.BillingType);
            worksheet.Cell(rowIndex, 4).Value = subscription.Amount;
            worksheet.Cell(rowIndex, 5).Value = financeResources.GetCurrencyLabel(subscription.Currency);
            worksheet.Cell(rowIndex, 6).Value = FormatDate(subscription.StartDate);
            worksheet.Cell(rowIndex, 7).Value = FormatDate(subscription.EndDate);
            worksheet.Cell(rowIndex, 8).Value = financeResources.GetStatusLabel(subscription.Status);
            rowIndex++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string FormatDate(DateOnly? date) =>
        date?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
}
