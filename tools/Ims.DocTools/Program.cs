using System.Text;
using ClosedXML.Excel;

// Ims.DocTools: small documentation helpers.
//   dotnet run --project tools/Ims.DocTools -- test-sheet
// turns docs/testing/test-cases.csv into docs/testing/test-cases.xlsx (coloured Pass/Fail, frozen header, filters).

if (args.Length == 0 || args[0] != "test-sheet")
{
    Console.WriteLine("Usage: dotnet run --project tools/Ims.DocTools -- test-sheet [csvPath] [xlsxPath]");
    return 1;
}

var root = FindRepoRoot();
var csvPath = args.Length > 1 ? args[1] : Path.Combine(root, "docs", "testing", "test-cases.csv");
var xlsxPath = args.Length > 2 ? args[2] : Path.Combine(root, "docs", "testing", "test-cases.xlsx");

var rows = ParseCsv(File.ReadAllText(csvPath, Encoding.UTF8).TrimStart('﻿'));
if (rows.Count < 2) { Console.Error.WriteLine("The CSV has no data rows."); return 1; }

using var workbook = new XLWorkbook();
var sheet = workbook.Worksheets.Add("Test cases");
for (var r = 0; r < rows.Count; r++)
    for (var c = 0; c < rows[r].Count; c++)
        sheet.Cell(r + 1, c + 1).Value = rows[r][c]; // assigning a string stores TEXT (never a formula)

var columns = rows[0].Count;
var statusColumn = rows[0].FindIndex(h => h.StartsWith("Status", StringComparison.OrdinalIgnoreCase)) + 1;
var header = sheet.Range(1, 1, 1, columns);
header.Style.Font.Bold = true;
header.Style.Fill.BackgroundColor = XLColor.FromHtml("#212529");
header.Style.Font.FontColor = XLColor.White;
sheet.SheetView.FreezeRows(1);
sheet.Range(1, 1, rows.Count, columns).SetAutoFilter();

// Column widths and wrapped text so long steps stay readable.
var widths = new[] { 9, 16, 30, 20, 26, 44, 44, 38, 12, 52 };
for (var c = 1; c <= columns; c++) sheet.Column(c).Width = c <= widths.Length ? widths[c - 1] : 30;
sheet.Range(1, 1, rows.Count, columns).Style.Alignment.WrapText = true;
sheet.Range(1, 1, rows.Count, columns).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

// Colour the status cell: green Pass, red Fail, amber anything else (e.g. Not run).
int pass = 0, fail = 0, other = 0;
for (var r = 2; r <= rows.Count; r++)
{
    var cell = sheet.Cell(r, statusColumn);
    var status = cell.GetString().Trim();
    var (back, fore) = status switch
    {
        "Pass" => ("#C6EFCE", "#006100"),
        "Fail" => ("#FFC7CE", "#9C0006"),
        _ => ("#FFEB9C", "#9C5700"),
    };
    cell.Style.Fill.BackgroundColor = XLColor.FromHtml(back);
    cell.Style.Font.FontColor = XLColor.FromHtml(fore);
    cell.Style.Font.Bold = true;
    if (status == "Pass") pass++; else if (status == "Fail") fail++; else other++;
}

// A small summary sheet.
var summary = workbook.Worksheets.Add("Summary");
summary.Cell(1, 1).Value = "Inventory Management System - test-case sheet";
summary.Cell(1, 1).Style.Font.Bold = true;
summary.Cell(3, 1).Value = "Cases"; summary.Cell(3, 2).Value = rows.Count - 1;
summary.Cell(4, 1).Value = "Pass"; summary.Cell(4, 2).Value = pass;
summary.Cell(5, 1).Value = "Fail"; summary.Cell(5, 2).Value = fail;
summary.Cell(6, 1).Value = "Not run / other"; summary.Cell(6, 2).Value = other;
summary.Columns().AdjustToContents();

Directory.CreateDirectory(Path.GetDirectoryName(xlsxPath)!);
workbook.SaveAs(xlsxPath);
Console.WriteLine($"Wrote {xlsxPath}: {rows.Count - 1} cases ({pass} pass, {fail} fail, {other} not run).");
return 0;

// ---- helpers ----

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs"))) dir = dir.Parent;
    return dir?.FullName ?? Directory.GetCurrentDirectory();
}

/// <summary>RFC 4180: quoted fields may contain commas, line breaks and doubled quotes.</summary>
static List<List<string>> ParseCsv(string text)
{
    var rows = new List<List<string>>();
    var row = new List<string>();
    var field = new StringBuilder();
    var quoted = false;
    for (var i = 0; i < text.Length; i++)
    {
        var ch = text[i];
        if (quoted)
        {
            if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
            else if (ch == '"') quoted = false;
            else field.Append(ch);
        }
        else if (ch == '"') quoted = true;
        else if (ch == ',') { row.Add(field.ToString()); field.Clear(); }
        else if (ch == '\r') { }
        else if (ch == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add(row); row = []; }
        else field.Append(ch);
    }
    if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
    return rows;
}
