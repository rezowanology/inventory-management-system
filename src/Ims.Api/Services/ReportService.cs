using Ims.Api.Contracts;
using Ims.Api.Data;
using Ims.Api.Domain;
using Ims.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ims.Api.Services;

/// <summary>
/// The three reports. Every number is derived from the append-only history (never from a stored counter) and counts
/// only what is still in effect, so a reversed delivery makes the unit show up in the stock report again.
/// </summary>
public class ReportService(ImsDbContext db, InventoryService inventory, AppTime time, IClock clock)
{
    // =============================== stock details ===============================

    /// <summary>All units that are InStock right now, filtered and sorted, as an unpaged list (the export uses this directly).</summary>
    public async Task<(List<StockRowDto> Rows, List<PointCountDto> ByPoint)> StockAsync(StockReportQuery query, CancellationToken ct)
    {
        // One query for the effective rows of every unit (demo scale, A10), then group per unit in memory.
        var effective = await db.InventoryTransactions.AsNoTracking()
            .Where(t => t.EventType != EventType.REVERSED && !db.InventoryTransactions.Any(r => r.ReversesId == t.Id))
            .Select(t => new
            {
                t.Id, t.UnitId, UnitBarcode = t.Unit!.Barcode, t.Unit.ProductId, ProductName = t.Unit.Product!.Name,
                t.EventType, t.PointId, PointName = t.Point!.Name, t.OccurredAt,
                By = t.PerformedBy!.EmployeeNo + " " + t.PerformedBy.FirstName + " " + t.PerformedBy.LastName,
            })
            .ToListAsync(ct);

        var now = clock.UtcNow;
        var inStock = new List<(StockRowDto Row, long ProductId)>();
        foreach (var unit in effective.GroupBy(t => t.UnitId))
        {
            var ordered = unit.OrderBy(t => t.OccurredAt).ThenBy(t => t.Id).ToList();
            var last = ordered.LastOrDefault(t => t.EventType is EventType.RECEIVED or EventType.DELIVERED);
            if (last?.EventType != EventType.RECEIVED) continue; // not in stock
            inStock.Add((new StockRowDto(last.UnitBarcode, last.ProductName, last.PointId, last.PointName, last.OccurredAt, last.By,
                Math.Max(0, (int)Math.Floor((now - last.OccurredAt).TotalDays)),
                ordered.Count(t => t.EventType == EventType.COMPONENT_TRACKED)), last.ProductId));
        }

        IEnumerable<(StockRowDto Row, long ProductId)> filtered = inStock;
        if (query.ProductId is { } product) filtered = filtered.Where(x => x.ProductId == product);
        if (query.MinDays is { } min) filtered = filtered.Where(x => x.Row.DaysInStock >= min);
        if (query.SearchText is { } text)
            filtered = filtered.Where(x => x.Row.Barcode.Contains(text, StringComparison.OrdinalIgnoreCase)
                || x.Row.ProductName.Contains(text, StringComparison.OrdinalIgnoreCase));

        // The chips ignore the point filter so that they stay usable as a way to switch point.
        var byPoint = filtered.GroupBy(x => (x.Row.PointId, x.Row.PointName))
            .Select(g => new PointCountDto(g.Key.PointId, g.Key.PointName, g.Count()))
            .OrderBy(p => p.PointName).ToList();

        if (query.PointId is { } point) filtered = filtered.Where(x => x.Row.PointId == point);
        var rows = filtered.Select(x => x.Row);
        var sorted = query.Sort switch
        {
            "days" => rows.OrderBy(r => r.DaysInStock).ThenBy(r => r.Barcode),
            "barcode" => rows.OrderBy(r => r.Barcode),
            "-barcode" => rows.OrderByDescending(r => r.Barcode),
            _ => rows.OrderByDescending(r => r.DaysInStock).ThenBy(r => r.Barcode), // default: longest in stock first
        };
        return (sorted.ToList(), byPoint);
    }

    public static ExportTable StockExport(IEnumerable<StockRowDto> rows, AppTime time) => new("stock-report",
        ["Barcode", "Product", "Receive point", "Received at", "Received by", "Days in stock", "Components"],
        rows.Select(r => new object?[] { r.Barcode, r.ProductName, r.PointName, time.Format(r.ReceivedAt), r.ReceivedBy, r.DaysInStock, r.ComponentCount }).ToList());

    // =============================== date-range summary ===============================

    /// <summary>
    /// Counts per local day. Validates the range (from &lt;= to, at most 366 days) and throws a DomainException with field errors otherwise.
    /// </summary>
    public async Task<(DateOnly From, DateOnly To, List<SummaryDayDto> Days)> SummaryAsync(SummaryReportQuery query, CancellationToken ct)
    {
        var to = query.To ?? time.LocalDay(clock.UtcNow);
        var from = query.From ?? to.AddDays(-29);

        var errors = new Dictionary<string, string[]>();
        if (from > to) errors["from"] = ["The start date must be on or before the end date."];
        else if (to.DayNumber - from.DayNumber + 1 > 366) errors["to"] = ["The range can be at most 366 days."];
        if (errors.Count > 0)
            throw new DomainException("invalid-range", errors.Values.First()[0]) { Errors = errors };

        var startUtc = time.StartOfDayUtc(from);
        var endUtc = time.StartOfDayUtc(to.AddDays(1));
        IQueryable<InventoryTransaction> rows = db.InventoryTransactions.AsNoTracking()
            .Where(t => t.OccurredAt >= startUtc && t.OccurredAt < endUtc);
        if (query.PointId is { } point) rows = rows.Where(t => t.PointId == point);
        if (query.ProductId is { } product) rows = rows.Where(t => t.Unit!.ProductId == product);

        var events = await rows.Select(t => new { t.OccurredAt, t.EventType }).ToListAsync(ct);
        var perDay = events.GroupBy(e => time.LocalDay(e.OccurredAt)).ToDictionary(g => g.Key, g => g.ToList());

        // Every day of the range appears (zeros included) so the chart has no gaps.
        var days = new List<SummaryDayDto>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var list = perDay.GetValueOrDefault(d) ?? [];
            days.Add(new SummaryDayDto(d,
                list.Count(e => e.EventType == EventType.COMPONENT_TRACKED), list.Count(e => e.EventType == EventType.RECEIVED),
                list.Count(e => e.EventType == EventType.DELIVERED), list.Count(e => e.EventType == EventType.REVERSED)));
        }
        return (from, to, days);
    }

    public static SummaryTotalsDto Totals(IReadOnlyList<SummaryDayDto> days) =>
        new(days.Sum(d => d.Tracked), days.Sum(d => d.Received), days.Sum(d => d.Delivered), days.Sum(d => d.Reversed));

    public static ExportTable SummaryExport(IEnumerable<SummaryDayDto> days) => new("summary-report",
        ["Date", "Tracked", "Received", "Delivered", "Reversed"],
        days.Select(d => new object?[] { d.Day.ToString("yyyy-MM-dd"), d.Tracked, d.Received, d.Delivered, d.Reversed }).ToList());

    // =============================== barcode-wise history ===============================

    /// <summary>
    /// Finds the unit for a unit barcode or a component barcode (the component may have been attached to several units over time).
    /// Returns the unit's complete timeline (reversed rows and REVERSED rows included), oldest first.
    /// </summary>
    public async Task<BarcodeReportDto> BarcodeAsync(string? barcode, CancellationToken ct)
    {
        var code = Barcode.Normalize(barcode);
        if (!Barcode.IsValid(code))
            throw new DomainException("invalid-barcode", "Barcode must be 6-32 characters: A-Z, 0-9 and \"-\".")
            { Errors = new Dictionary<string, string[]> { ["barcode"] = ["Barcode must be 6-32 characters: A-Z, 0-9 and \"-\"."] } };

        var unitCode = await db.Units.AsNoTracking().Where(u => u.Barcode == code).Select(u => u.Barcode).FirstOrDefaultAsync(ct);
        if (unitCode is null)
        {
            // Not a unit barcode: was it ever a component barcode? (reversed rows count: the history is complete.)
            var matches = await db.InventoryTransactions.AsNoTracking()
                .Where(t => t.EventType == EventType.COMPONENT_TRACKED && t.ComponentBarcode == code)
                .Select(t => t.Unit!.Barcode).Distinct().ToListAsync(ct);
            if (matches.Count == 0) return new BarcodeReportDto(code, false, null, [], []);
            if (matches.Count > 1)
            {
                var options = new List<UnitMatchDto>();
                foreach (var m in matches.Order())
                {
                    var lookup = await inventory.LookupAsync(m, ct);
                    options.Add(new UnitMatchDto(m, lookup.Product ?? "", lookup.Status));
                }
                return new BarcodeReportDto(code, true, null, options, []);
            }
            unitCode = matches[0];
        }

        var unit = await inventory.LookupAsync(unitCode, ct);
        var timeline = await db.InventoryTransactions.AsNoTracking()
            .Where(t => t.Unit!.Barcode == unitCode)
            .OrderBy(t => t.OccurredAt).ThenBy(t => t.Id)
            .ToDtos(db).ToListAsync(ct);
        return new BarcodeReportDto(code, true, unit, [], timeline);
    }
}
