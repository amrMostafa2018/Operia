using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Operia.Application.Customers.Queries.ListCustomerOperations;

namespace Operia.Application.Customers.Queries.ExportCustomerOperations;

internal static class CustomerOperationsWorkbook
{
    private static readonly string[] Headers =
    [
        "#", "Customer", "Mobile", "Branch", "Service", "Service Type",
        "Payment Type", "Total", "Collected", "Refunded", "Net Collected", "Remaining", "Units",
        "Customer Restriction", "Purchase Status", "Payment Status", "Refund Status",
        "Installment Due", "Operation Date"
    ];

    public static byte[] Write(IReadOnlyList<CustomerOperationDto> rows, string timezoneId)
    {
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddText(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>
                """);
            AddText(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            AddText(zip, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Customer Operations" sheetId="1" r:id="rId1"/></sheets></workbook>
                """);
            AddText(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
                """);
            AddText(zip, "xl/styles.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="1"><font><sz val="11"/><name val="Aptos"/></font></fonts><fills count="1"><fill><patternFill patternType="none"/></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf numFmtId="0"/></cellStyleXfs><cellXfs count="2"><xf numFmtId="0" xfId="0"/><xf numFmtId="14" xfId="0" applyNumberFormat="1"/></cellXfs></styleSheet>
                """);
            var entry = zip.CreateEntry("xl/worksheets/sheet1.xml");
            using var stream = entry.Open();
            using var writer = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                CloseOutput = false
            });
            writer.WriteStartDocument();
            writer.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            writer.WriteStartElement("sheetData");
            WriteRow(writer, 1, Headers.Select(x => (object?)x).ToArray());
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                WriteRow(writer, index + 2,
                [
                    index + 1, row.CustomerName, row.Mobile, row.BranchName, row.Service,
                    row.ServiceType, row.PaymentType, row.Total, row.Collected,
                    row.Refunded, row.NetCollected, row.Remaining,
                    row.UnitsRemaining is null ? "—" : $"{row.UnitsRemaining} / {row.UnitsTotal}",
                    row.CustomerRestriction, row.PurchaseStatus, row.PaymentStatus, row.RefundStatus,
                    row.InstallmentDueStatus,
                    TimeZoneInfo.ConvertTimeFromUtc(
                        DateTime.SpecifyKind(row.OperationDate, DateTimeKind.Utc), timezone)
                ]);
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
        return output.ToArray();
    }

    private static void WriteRow(XmlWriter writer, int number, IReadOnlyList<object?> values)
    {
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", number.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", $"{Column(index)}{number}");
            if (value is DateTime date)
            {
                writer.WriteAttributeString("s", "1");
                writer.WriteElementString("v", date.ToOADate().ToString(CultureInfo.InvariantCulture));
            }
            else if (value is decimal or int)
            {
                writer.WriteElementString("v", Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            else
            {
                writer.WriteAttributeString("t", "inlineStr");
                writer.WriteStartElement("is");
                writer.WriteElementString("t", value?.ToString() ?? string.Empty);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static string Column(int index)
    {
        var name = string.Empty;
        for (var value = index + 1; value > 0; value = (value - 1) / 26)
        {
            name = (char)('A' + (value - 1) % 26) + name;
        }
        return name;
    }

    private static void AddText(ZipArchive zip, string path, string contents)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(contents);
    }
}
