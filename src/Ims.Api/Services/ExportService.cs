using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace Ims.Api.Services;

/// <summary>A report ready to be written to a file: a name, header cells and rows of plain values (string / number / null).</summary>
public record ExportTable(string Report, IReadOnlyList<string> Headers, IReadOnlyList<object?[]> Rows);

/// <summary>
/// Turns an <see cref="ExportTable"/> into a CSV or Excel download. Controllers build the table from the SAME filtered
/// query as the on-screen list (just without paging), so the file always matches what the user was looking at.
/// </summary>
public class ExportService(AppTime time, IClock clock)
{
    /// <summary>The download for ?format=csv|xlsx, named &lt;report&gt;_&lt;yyyyMMdd-HHmm&gt;.&lt;ext&gt; (local time). Any other format is a 400.</summary>
    public ActionResult Create(ExportTable table, string? format)
    {
        var stamp = time.ToLocal(clock.UtcNow).ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        switch (format?.Trim().ToLowerInvariant())
        {
            case "csv":
                return new FileContentResult(ToCsv(table), "text/csv; charset=utf-8") { FileDownloadName = $"{table.Report}_{stamp}.csv" };
            case "xlsx":
                return new FileContentResult(ToXlsx(table), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                    { FileDownloadName = $"{table.Report}_{stamp}.xlsx" };
            default:
                return new BadRequestObjectResult(new ProblemDetails
                    { Status = 400, Title = "Unknown export format", Detail = "Use format=csv or format=xlsx." });
        }
    }

    /// <summary>RFC 4180: comma separated, CRLF line ends, fields with commas / quotes / line breaks wrapped in quotes, quotes doubled. UTF-8 with a BOM so Excel reads accents correctly.</summary>
    public static byte[] ToCsv(ExportTable table)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', table.Headers.Select(Escape))).Append("\r\n");
        foreach (var row in table.Rows)
            sb.Append(string.Join(',', row.Select(v => Escape(ToText(v, guardFormulas: true))))).Append("\r\n");
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    /// <summary>One worksheet: bold header, frozen first row, auto-filter and column widths that fit the content.</summary>
    public static byte[] ToXlsx(ExportTable table)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName(table.Report));

        for (var c = 0; c < table.Headers.Count; c++) sheet.Cell(1, c + 1).Value = table.Headers[c];
        for (var r = 0; r < table.Rows.Count; r++)
            for (var c = 0; c < table.Headers.Count; c++)
            {
                var cell = sheet.Cell(r + 2, c + 1);
                switch (table.Rows[r][c])
                {
                    case null: break;
                    case int i: cell.Value = i; break;
                    case long l: cell.Value = l; break;
                    case double d: cell.Value = d; break;
                    case bool b: cell.Value = b; break;
                    case var other: cell.Value = other.ToString() ?? ""; break; // assigning a string stores TEXT (never a formula)
                }
            }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        if (table.Headers.Count > 0)
            sheet.Range(1, 1, Math.Max(1, table.Rows.Count + 1), table.Headers.Count).SetAutoFilter();
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string SheetName(string report) => report.Length <= 31 ? report : report[..31]; // Excel's limit

    private static string ToText(object? value, bool guardFormulas)
    {
        var text = value switch
        {
            null => "",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
        // CSV injection guard: Excel would run a cell that starts with = + - @ as a formula. A leading ' makes it text.
        if (guardFormulas && value is string && text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
        return text;
    }

    private static string Escape(string field) =>
        field.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + field.Replace("\"", "\"\"") + "\"" : field;
}
