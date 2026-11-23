using Ims.Api.Authorization;
using Ims.Api.Contracts;
using Ims.Api.Infrastructure;
using Ims.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ims.Api.Controllers;

/// <summary>
/// The three reports (Admin and Report Viewer). Thin: the numbers come from ReportService; each action either
/// returns JSON for the screen or, with ?format=csv|xlsx, the same data as a file.
/// </summary>
[Route("api/reports")]
[Authorize(Policy = Capabilities.ReportsView)]
public class ReportsController(ReportService reports, ExportService export, AppTime time) : ApiControllerBase
{
    /// <summary>Units in stock right now, one row each. Filters: product, point, minDays; search barcode; sort days|-days|barcode.</summary>
    [HttpGet("stock")]
    public async Task<ActionResult<StockReportDto>> Stock([FromQuery] StockReportQuery query, CancellationToken ct)
    {
        var (rows, byPoint) = await reports.StockAsync(query, ct);
        if (query.Format is not null) return export.Create(ReportService.StockExport(rows, time), query.Format);

        var items = rows.Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize).ToList();
        return new StockReportDto(items, query.SafePage, query.SafePageSize, rows.Count, byPoint);
    }

    /// <summary>Counts per day between from and to (local days, at most 366). Filters: point, product.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<SummaryReportDto>> Summary([FromQuery] SummaryReportQuery query, CancellationToken ct)
    {
        var (from, to, days) = await reports.SummaryAsync(query, ct); // a bad range throws DomainException -> 422 with field errors
        if (query.Format is not null) return export.Create(ReportService.SummaryExport(days), query.Format);

        var items = days.Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize).ToList();
        return new SummaryReportDto(from, to, ReportService.Totals(days), days, items, query.SafePage, query.SafePageSize, days.Count);
    }

    /// <summary>The full history of one unit, found by its barcode or by a component barcode.</summary>
    [HttpGet("barcode")]
    public async Task<ActionResult<BarcodeReportDto>> Barcode([FromQuery] string? barcode, [FromQuery] string? format, CancellationToken ct)
    {
        var report = await reports.BarcodeAsync(barcode, ct);
        if (format is null) return report;

        if (report.Unit is null)
            throw new DomainException("no-unit", report.Matches.Count > 1
                ? "This component was attached to several units: pick one unit barcode to export."
                : "No unit or component has this barcode.");
        return export.Create(HistoryQueries.ToExport($"barcode-{report.Unit.Barcode}", report.Timeline, time), format);
    }
}
