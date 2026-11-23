using Ims.Api.Infrastructure;

namespace Ims.Api.Contracts;

// ---- Stock report ----

public class StockReportQuery : ListQuery
{
    public long? ProductId { get; set; }
    public long? PointId { get; set; }
    /// <summary>Only units that have been in stock for at least this many days.</summary>
    public int? MinDays { get; set; }
}

public record StockRowDto(
    string Barcode, string ProductName, long PointId, string PointName, DateTime ReceivedAt,
    string ReceivedBy, int DaysInStock, int ComponentCount);

public record PointCountDto(long PointId, string PointName, int Count);

/// <summary>A page of the stock report plus the per-receive-point chips (computed WITHOUT the point filter, so they can be used to pick one).</summary>
public record StockReportDto(
    IReadOnlyList<StockRowDto> Items, int Page, int PageSize, int TotalCount, IReadOnlyList<PointCountDto> ByPoint);

// ---- Summary report ----

public class SummaryReportQuery : ListQuery
{
    /// <summary>First local day, "yyyy-MM-dd". Default: 29 days before <c>To</c> (a 30-day window).</summary>
    public DateOnly? From { get; set; }
    /// <summary>Last local day, inclusive. Default: today.</summary>
    public DateOnly? To { get; set; }
    public long? PointId { get; set; }
    public long? ProductId { get; set; }
}

public record SummaryDayDto(DateOnly Day, int Tracked, int Received, int Delivered, int Reversed);

public record SummaryTotalsDto(int Tracked, int Received, int Delivered, int Reversed);

/// <summary><c>Chart</c> has every day of the range (for the graph); <c>Items</c> is one page of the same days (for the table).</summary>
public record SummaryReportDto(
    DateOnly From, DateOnly To, SummaryTotalsDto Totals, IReadOnlyList<SummaryDayDto> Chart,
    IReadOnlyList<SummaryDayDto> Items, int Page, int PageSize, int TotalCount);

// ---- Barcode history ----

public record UnitMatchDto(string Barcode, string ProductName, string Status);

/// <summary>
/// Answer of the barcode report. A unit barcode finds its unit directly; a component barcode finds every unit it was
/// EVER attached to: one match is shown at once, several are listed in <c>Matches</c> so the user can pick.
/// </summary>
public record BarcodeReportDto(
    string Query, bool Found, UnitLookupDto? Unit, IReadOnlyList<UnitMatchDto> Matches, IReadOnlyList<HistoryRowDto> Timeline);
