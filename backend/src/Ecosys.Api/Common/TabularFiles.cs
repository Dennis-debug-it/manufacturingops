using System.Data;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Ecosys.Shared.Errors;
using ExcelDataReader;

namespace Ecosys.Api.Common;

/// <summary>A sheet to write: a name, a header row, and data rows.</summary>
public sealed record SheetSpec(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string?>> Rows);

/// <summary>
/// Reads the first sheet of an .xlsx, .xls or .csv upload into rows keyed by header, and writes
/// simple .xlsx files for import templates.
/// </summary>
public static class TabularFiles
{
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const int MaxRows = 5000;

    static TabularFiles()
    {
        // ExcelDataReader needs the legacy code pages for older .xls files.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>Rows keyed by trimmed header (case-insensitive); blank rows are skipped.</summary>
    public static IReadOnlyList<IReadOnlyDictionary<string, string?>> Read(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        using var reader = extension switch
        {
            ".xlsx" or ".xls" => ExcelReaderFactory.CreateReader(stream),
            ".csv" => ExcelReaderFactory.CreateCsvReader(stream, new ExcelReaderConfiguration { FallbackEncoding = Encoding.UTF8 }),
            _ => throw new BusinessRuleException("Upload an Excel (.xlsx) or CSV file.")
        };

        var table = reader.AsDataSet().Tables.Cast<DataTable>().FirstOrDefault();
        if (table is null || table.Rows.Count == 0)
        {
            return [];
        }

        var headers = table.Rows[0].ItemArray.Select(x => Convert.ToString(x)?.Trim().TrimEnd('*') ?? string.Empty).ToList();
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        for (var i = 1; i < table.Rows.Count; i++)
        {
            var values = table.Rows[i].ItemArray;
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count; c++)
            {
                if (string.IsNullOrWhiteSpace(headers[c]))
                {
                    continue;
                }

                row[headers[c]] = c < values.Length ? Format(values[c]) : null;
            }

            if (row.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            if (rows.Count == MaxRows)
            {
                throw new BusinessRuleException($"Import at most {MaxRows} rows at a time.");
            }

            rows.Add(row);
        }

        return rows;
    }

    public static byte[] WriteXlsx(IReadOnlyList<SheetSpec> sheets)
    {
        using var output = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(output, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            workbook.Workbook = new Workbook();
            var sheetList = workbook.Workbook.AppendChild(new Sheets());
            var styles = workbook.AddNewPart<WorkbookStylesPart>();
            styles.Stylesheet = BoldHeaderStylesheet();

            for (var index = 0; index < sheets.Count; index++)
            {
                var sheet = sheets[index];
                var part = workbook.AddNewPart<WorksheetPart>();
                var data = new SheetData();
                data.Append(BuildRow(1, sheet.Headers, bold: true));
                for (var r = 0; r < sheet.Rows.Count; r++)
                {
                    data.Append(BuildRow((uint)r + 2, sheet.Rows[r], bold: false));
                }

                var columns = new Columns(sheet.Headers.Select((h, i) =>
                    new Column { Min = (uint)i + 1, Max = (uint)i + 1, Width = Math.Clamp(h.Length + 4, 12, 40), CustomWidth = true }));
                part.Worksheet = new Worksheet(columns, data);
                sheetList.Append(new Sheet { Id = workbook.GetIdOfPart(part), SheetId = (uint)index + 1, Name = sheet.Name });
            }
        }

        return output.ToArray();
    }

    private static string? Format(object? value) => value switch
    {
        null or DBNull => null,
        DateTime date => date.TimeOfDay == TimeSpan.Zero ? date.ToString("yyyy-MM-dd") : date.ToString("yyyy-MM-dd HH:mm"),
        double number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)?.Trim()
    };

    private static Row BuildRow(uint index, IEnumerable<string?> values, bool bold)
    {
        var row = new Row { RowIndex = index };
        foreach (var value in values)
        {
            row.Append(new Cell
            {
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(value ?? string.Empty)),
                StyleIndex = bold ? 1U : 0U
            });
        }

        return row;
    }

    private static Stylesheet BoldHeaderStylesheet() => new(
        new Fonts(new Font(), new Font(new Bold())) { Count = 2 },
        new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 })) { Count = 2 },
        new Borders(new Border()) { Count = 1 },
        new CellFormats(new CellFormat(), new CellFormat { FontId = 1, ApplyFont = true }) { Count = 2 });
}
