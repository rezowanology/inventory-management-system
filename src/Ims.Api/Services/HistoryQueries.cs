using Ims.Api.Contracts;
using Ims.Api.Data;
using Ims.Api.Domain;

namespace Ims.Api.Services;

/// <summary>The one place that turns history rows into <see cref="HistoryRowDto"/> (history list, barcode report, exports).</summary>
public static class HistoryQueries
{
    /// <summary>
    /// A LINQ projection: EF Core translates it into a single SQL query. The two sub-queries look up the REVERSED row
    /// (if any) whose reverses_id points at this row, so the list can show "reversed by X on Y".
    /// </summary>
    public static IQueryable<HistoryRowDto> ToDtos(this IQueryable<InventoryTransaction> rows, ImsDbContext db) =>
        rows.Select(t => new HistoryRowDto(
            t.Id, t.Unit!.Barcode, t.Unit.Product!.Name, t.EventType.ToString(), t.Point!.Name,
            t.Component != null ? t.Component.Name : null, t.ComponentBarcode, t.ReversesId, t.Reason,
            t.PerformedBy!.EmployeeNo, t.PerformedBy.FirstName + " " + t.PerformedBy.LastName, t.OccurredAt,
            db.InventoryTransactions.Where(r => r.ReversesId == t.Id).Select(r => (long?)r.Id).FirstOrDefault(),
            db.InventoryTransactions.Where(r => r.ReversesId == t.Id)
                .Select(r => r.PerformedBy!.EmployeeNo + " " + r.PerformedBy.FirstName + " " + r.PerformedBy.LastName).FirstOrDefault(),
            db.InventoryTransactions.Where(r => r.ReversesId == t.Id).Select(r => (DateTime?)r.OccurredAt).FirstOrDefault()));

    /// <summary>The columns of every history export (history list and barcode report).</summary>
    public static ExportTable ToExport(string report, IEnumerable<HistoryRowDto> rows, AppTime time) => new(report,
        ["Time", "Barcode", "Product", "Event", "Point", "Component", "Component barcode", "By (employee no.)", "By", "Reason",
         "Reverses row", "Reversed by", "Reversed at"],
        rows.Select(r => new object?[]
        {
            time.Format(r.OccurredAt), r.UnitBarcode, r.ProductName, r.EventType, r.PointName, r.ComponentName, r.ComponentBarcode,
            r.PerformedByEmployeeNo, r.PerformedByName, r.Reason, r.ReversesId, r.ReversedBy, time.Format(r.ReversedAt),
        }).ToList());
}
