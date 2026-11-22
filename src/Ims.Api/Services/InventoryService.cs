using Ims.Api.Data;
using Ims.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ims.Api.Services;

public enum UnitState { NotFound, Registered, InStock, Delivered }

/// <summary>A unit's derived status. Point/Since are filled for InStock (receive point, since when) and Delivered (delivery point, at).</summary>
public record UnitStatus(UnitState State, string? PointName = null, DateTime? Since = null);

/// <summary>
/// ALL inventory rules live here (controllers stay thin). Status is never stored: it is derived from the
/// append-only history (assumption A10). Day 1 has the read side; Track/Receive/Deliver arrive on Day 3
/// and Reverse on Day 4.
/// </summary>
public class InventoryService(ImsDbContext db)
{
    /// <summary>Trim + upper-case (A13). Validation against the pattern is a separate step.</summary>
    public static string NormalizeBarcode(string? raw) => Barcode.Normalize(raw);

    /// <summary>
    /// The rows still "in effect" for a unit: not a REVERSED row, and not cancelled by one.
    /// Ordered by (occurred_at, id) so the last row is the latest.
    /// </summary>
    public async Task<List<InventoryTransaction>> GetEffectiveRowsAsync(long unitId, CancellationToken ct = default)
    {
        return await EffectiveRows()
            .Where(t => t.UnitId == unitId)
            .Include(t => t.Point)
            .Include(t => t.Component)
            .Include(t => t.PerformedBy)
            .OrderBy(t => t.OccurredAt).ThenBy(t => t.Id)
            .ToListAsync(ct);
    }

    /// <summary>Status = decided by the last effective RECEIVED / DELIVERED row.</summary>
    public async Task<UnitStatus> GetUnitStatusAsync(string barcode, CancellationToken ct = default)
    {
        var normalized = NormalizeBarcode(barcode);
        var unit = await db.Units.AsNoTracking().FirstOrDefaultAsync(u => u.Barcode == normalized, ct);
        if (unit is null) return new UnitStatus(UnitState.NotFound);

        var last = (await GetEffectiveRowsAsync(unit.Id, ct))
            .LastOrDefault(t => t.EventType is EventType.RECEIVED or EventType.DELIVERED);

        return last?.EventType switch
        {
            EventType.RECEIVED => new UnitStatus(UnitState.InStock, last.Point!.Name, last.OccurredAt),
            EventType.DELIVERED => new UnitStatus(UnitState.Delivered, last.Point!.Name, last.OccurredAt),
            _ => new UnitStatus(UnitState.Registered)
        };
    }

    /// <summary>The components currently attached to a unit = its effective COMPONENT_TRACKED rows.</summary>
    public async Task<List<InventoryTransaction>> GetAttachedComponentsAsync(string barcode, CancellationToken ct = default)
    {
        var normalized = NormalizeBarcode(barcode);
        var unit = await db.Units.AsNoTracking().FirstOrDefaultAsync(u => u.Barcode == normalized, ct);
        if (unit is null) return [];
        return (await GetEffectiveRowsAsync(unit.Id, ct)).Where(t => t.EventType == EventType.COMPONENT_TRACKED).ToList();
    }

    /// <summary>Current stock = number of units whose status is InStock.</summary>
    public async Task<int> CountInStockAsync(CancellationToken ct = default)
    {
        // Latest effective RECEIVED/DELIVERED row per unit; stock = those whose latest is RECEIVED.
        var lastEvents = await EffectiveRows()
            .Where(t => t.EventType == EventType.RECEIVED || t.EventType == EventType.DELIVERED)
            .Select(t => new { t.UnitId, t.EventType, t.OccurredAt, t.Id })
            .ToListAsync(ct);
        return lastEvents
            .GroupBy(t => t.UnitId)
            .Count(g => g.OrderBy(t => t.OccurredAt).ThenBy(t => t.Id).Last().EventType == EventType.RECEIVED);
    }

    // Query: rows that are not REVERSED and are not referenced by a REVERSED row.
    private IQueryable<InventoryTransaction> EffectiveRows() =>
        db.InventoryTransactions.AsNoTracking().Where(t =>
            t.EventType != EventType.REVERSED &&
            !db.InventoryTransactions.Any(r => r.ReversesId == t.Id));

    // ---- Built on later days (kept as honest stubs) ----

    /// <summary>Day 3: attach components to a unit (rule R3). Registers the unit on first scan (R2).</summary>
    public Task TrackAsync() => throw new NotImplementedException("Built on Day 3");

    /// <summary>Day 3: receive a unit at a receive point (rule R4).</summary>
    public Task ReceiveAsync() => throw new NotImplementedException("Built on Day 3");

    /// <summary>Day 3: deliver an in-stock unit (rule R5).</summary>
    public Task DeliverAsync() => throw new NotImplementedException("Built on Day 3");

    /// <summary>Day 4: admin reversal with a reason (rules R7-R10).</summary>
    public Task ReverseAsync() => throw new NotImplementedException("Built on Day 4");
}
