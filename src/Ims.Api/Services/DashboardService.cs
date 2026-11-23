using Ims.Api.Contracts;
using Ims.Api.Data;
using Ims.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ims.Api.Services;

/// <summary>
/// The numbers on the dashboard. Everything is derived from the history (never from a stored counter), so a reversal
/// is reflected at once. "Today" is the local day of App:TimeZone.
/// </summary>
public class DashboardService(ImsDbContext db, ReportService reports, AppTime time, IClock clock)
{
    public async Task<DashboardResponse> GetMetricsAsync(bool includeRecent, CancellationToken ct = default)
    {
        var today = time.LocalDay(clock.UtcNow);
        var start = time.StartOfDayUtc(today);
        var end = time.StartOfDayUtc(today.AddDays(1));

        // Today's receives/deliveries count rows that are still in effect (a row reversed later today no longer counts);
        // the reversals themselves are counted separately.
        var todayRows = await db.InventoryTransactions.AsNoTracking()
            .Where(t => t.OccurredAt >= start && t.OccurredAt < end)
            .Select(t => new { t.EventType, Reversed = db.InventoryTransactions.Any(r => r.ReversesId == t.Id) })
            .ToListAsync(ct);
        var registeredToday = await db.Units.CountAsync(u => u.RegisteredAt >= start && u.RegisteredAt < end, ct);

        // Stock figures come from the same code as the stock report, so the two can never disagree.
        var (stock, byPoint) = await reports.StockAsync(new StockReportQuery(), ct); // default sort: longest in stock first

        var recent = includeRecent
            ? await db.InventoryTransactions.AsNoTracking()
                .OrderByDescending(t => t.OccurredAt).ThenByDescending(t => t.Id).Take(10).ToDtos(db).ToListAsync(ct)
            : [];

        return new DashboardResponse(
            await db.Units.CountAsync(ct), stock.Count,
            todayRows.Count(r => r.EventType == EventType.RECEIVED && !r.Reversed),
            todayRows.Count(r => r.EventType == EventType.DELIVERED && !r.Reversed),
            todayRows.Count(r => r.EventType == EventType.REVERSED),
            registeredToday, byPoint,
            stock.Take(10).Select(s => new StuckUnitDto(s.Barcode, s.ProductName, s.PointName, s.DaysInStock)).ToList(),
            recent);
    }
}
