using Operia.Application.Common.Models;

namespace Operia.Application.Finance.Export;

public static class TenantSubscriptionExportFileFactory
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static FileExportResult Create(byte[] content, string format)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        if (format.Trim().Equals("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return new FileExportResult(
                content,
                "application/pdf",
                $"operia-subscriptions-{timestamp}.pdf");
        }

        return new FileExportResult(
            content,
            ExcelContentType,
            $"operia-subscriptions-{timestamp}.xlsx");
    }
}
