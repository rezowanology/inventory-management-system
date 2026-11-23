using Ims.Api.Contracts;
using Ims.Api.Data;
using Ims.Api.Domain;
using Ims.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ims.Api.Services;

public enum UnitState { NotFound, Registered, InStock, Delivered }

/// <summary>A unit's derived status. Point/Since are filled for InStock (receive point, since when) and Delivered (delivery point, at).</summary>
public record UnitStatus(UnitState State, string? PointName = null, DateTime? Since = null);

/// <summary>
/// ALL inventory rules live here (controllers stay thin). Status is never stored: it is derived from the
/// append-only history (assumption A10). Reads (status, components, stock), the Day 3 write
/// operations Track/Receive/Deliver and the Day 4 admin reversal.
/// </summary>
public class InventoryService(ImsDbContext db, IClock clock)
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


    // =====================================================================================================
    // Day 3: the three scan operations. Every one follows the same recipe:
    //   1. normalize + validate the barcode          4. check the rules against the unit's derived status
    //   2. open ONE database transaction             5. add the history row(s) and save
    //   3. lock the unit row (SELECT ... FOR UPDATE) 6. commit, then answer with the unit's new state
    // The lock means two people scanning the same barcode at the same moment are handled one after the other,
    // so the second one sees the first one's result and gets a duplicate-scan warning instead of a double row.
    // =====================================================================================================

    /// <summary>One component scanned in a Track batch.</summary>
    public record TrackItem(long ComponentId, string ComponentBarcode);

    /// <summary>
    /// Attach components to a unit (rules R2, R3, A2). The first scan of an unknown barcode registers the unit
    /// (a product must be given). The whole batch is validated first; then ALL rows are written, or none.
    /// </summary>
    public async Task<UnitLookupDto> TrackAsync(
        string barcode, long? productId, long? trackPointId, IReadOnlyList<TrackItem> items, long userId, CancellationToken ct = default)
    {
        var code = RequireBarcode(barcode);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var unit = await LockUnitAsync(code, ct) ?? await NewUnitAsync(code, productId, userId, ct);
        var point = await RequirePointAsync(trackPointId, PointType.TRACK, "trackPointId", ct);
        var rows = await EffectiveRowsOfAsync(unit, ct);

        if (DeriveStatus(rows).State == UnitState.Delivered)
            throw Fail("unit-delivered", $"Unit {code} has already been delivered; components can no longer be tracked.");
        if (items.Count == 0)
            throw Fail("no-components", "Add at least one component.", "components");

        // ---- validate the whole batch before writing anything ----
        var normalizedCodes = items.Select(i => Barcode.Normalize(i.ComponentBarcode)).ToList();
        var componentIds = items.Select(i => i.ComponentId).Distinct().ToList();
        var components = await db.Components.AsNoTracking()
            .Where(c => componentIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        // Rows that currently hold these component barcodes (reversed rows are ignored: they are free again, R10).
        var taken = (await EffectiveRows()
                .Where(t => t.EventType == EventType.COMPONENT_TRACKED && normalizedCodes.Contains(t.ComponentBarcode!))
                .Include(t => t.Unit).Include(t => t.Point).Include(t => t.PerformedBy)
                .ToListAsync(ct))
            .ToDictionary(t => t.ComponentBarcode!);

        var errors = new Dictionary<string, List<string>>();
        void AddError(int index, string field, string message)
        {
            var key = $"components[{index}].{field}";
            if (!errors.TryGetValue(key, out var list)) errors[key] = list = [];
            list.Add(message);
        }

        InventoryTransaction? repeat = null; // a component already on THIS unit = warning, not error (A8)
        var seen = new HashSet<string>();
        for (var i = 0; i < items.Count; i++)
        {
            var cc = normalizedCodes[i];
            if (!components.TryGetValue(items[i].ComponentId, out var component) || component.IsDeleted)
                AddError(i, "componentId", "This component does not exist or has been archived.");

            if (!Barcode.IsValid(cc))
                AddError(i, "componentBarcode", "Barcode must be 6-32 characters: A-Z, 0-9 and \"-\".");
            else if (cc == code)
                AddError(i, "componentBarcode", "A component barcode cannot be the same as the product barcode.");
            else if (!seen.Add(cc))
                AddError(i, "componentBarcode", $"Component {cc} appears more than once in this request.");
            else if (taken.TryGetValue(cc, out var existing))
            {
                if (unit.Id != 0 && existing.UnitId == unit.Id) repeat ??= existing;
                else AddError(i, "componentBarcode", $"Component {cc} is already attached to unit {existing.Unit!.Barcode}");
            }
        }
        if (errors.Count > 0)
        {
            var messages = errors.Values.SelectMany(m => m).ToList();
            throw new DomainException("validation-failed",
                messages.Count == 1 ? messages[0] : $"{messages[0]} (and {messages.Count - 1} more problem(s))")
            { Errors = errors.ToDictionary(e => e.Key, e => e.Value.ToArray()) };
        }
        if (repeat is not null)
            throw new DuplicateScanException(
                $"Component {repeat.ComponentBarcode} is already attached to unit {code}.",
                repeat.OccurredAt, repeat.Point!.Name, UserLabel(repeat.PerformedBy!));

        // ---- write all rows (same timestamp for the whole batch) ----
        var now = clock.UtcNow;
        for (var i = 0; i < items.Count; i++)
            db.InventoryTransactions.Add(new InventoryTransaction
            {
                Unit = unit, EventType = EventType.COMPONENT_TRACKED, PointId = point.Id,
                ComponentId = items[i].ComponentId, ComponentBarcode = normalizedCodes[i],
                PerformedById = userId, OccurredAt = now,
            });
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await LookupAsync(code, ct);
    }

    /// <summary>Receive a unit at a receive point (rules R2, R4). Only a Registered unit can be received.</summary>
    public async Task<UnitLookupDto> ReceiveAsync(
        string barcode, long? productId, long? receivePointId, long userId, CancellationToken ct = default)
    {
        var code = RequireBarcode(barcode);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var unit = await LockUnitAsync(code, ct) ?? await NewUnitAsync(code, productId, userId, ct);
        var point = await RequirePointAsync(receivePointId, PointType.RECEIVE, "receivePointId", ct);
        var rows = await EffectiveRowsOfAsync(unit, ct);

        switch (DeriveStatus(rows).State)
        {
            case UnitState.InStock: // same state again = warning (A8): tell who/where/when, save nothing
                var last = rows.Last(t => t.EventType == EventType.RECEIVED);
                throw new DuplicateScanException($"Unit {code} is already in stock.",
                    last.OccurredAt, last.Point!.Name, UserLabel(last.PerformedBy!));
            case UnitState.Delivered:
                throw Fail("unit-delivered", $"Unit {code} has already been delivered. Reverse its delivery before receiving it again.");
        }

        db.InventoryTransactions.Add(new InventoryTransaction
        {
            Unit = unit, EventType = EventType.RECEIVED, PointId = point.Id, PerformedById = userId, OccurredAt = clock.UtcNow,
        });
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await LookupAsync(code, ct);
    }

    /// <summary>Deliver a unit that is in stock (rule R5). An unknown barcode is an error; it is never registered here.</summary>
    public async Task<UnitLookupDto> DeliverAsync(
        string barcode, long? deliveryPointId, long userId, CancellationToken ct = default)
    {
        var code = RequireBarcode(barcode);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var unit = await LockUnitAsync(code, ct)
            ?? throw Fail("unit-not-found", $"Unit {code} is not registered, so it cannot be delivered.", "barcode");
        var point = await RequirePointAsync(deliveryPointId, PointType.DELIVERY, "deliveryPointId", ct);
        var rows = await EffectiveRowsOfAsync(unit, ct);

        switch (DeriveStatus(rows).State)
        {
            case UnitState.Registered:
                throw Fail("not-in-stock", $"Unit {code} has not been received yet, so it is not in stock.", "barcode");
            case UnitState.Delivered:
                throw Fail("already-delivered", $"Unit {code} has already been delivered.", "barcode");
        }

        db.InventoryTransactions.Add(new InventoryTransaction
        {
            Unit = unit, EventType = EventType.DELIVERED, PointId = point.Id, PerformedById = userId, OccurredAt = clock.UtcNow,
        });
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await LookupAsync(code, ct);
    }

    /// <summary>
    /// Everything a scan screen shows about a barcode: product, derived status, attached components and the
    /// last 5 history rows (reversals included). An unknown barcode answers Exists=false / status NotFound.
    /// </summary>
    public async Task<UnitLookupDto> LookupAsync(string barcode, CancellationToken ct = default)
    {
        var code = NormalizeBarcode(barcode);
        var unit = await db.Units.AsNoTracking().Include(u => u.Product).FirstOrDefaultAsync(u => u.Barcode == code, ct);
        if (unit is null) return new UnitLookupDto(code, false, null, nameof(UnitState.NotFound), null, null, [], []);

        var effective = await GetEffectiveRowsAsync(unit.Id, ct);
        var status = DeriveStatus(effective);
        var components = effective
            .Where(t => t.EventType == EventType.COMPONENT_TRACKED)
            .Select(t => new AttachedComponentDto(t.Component!.Name, t.ComponentBarcode!, t.OccurredAt)).ToList();
        var recent = await db.InventoryTransactions.AsNoTracking()
            .Where(t => t.UnitId == unit.Id)
            .OrderByDescending(t => t.OccurredAt).ThenByDescending(t => t.Id).Take(5)
            .Select(t => new UnitEventDto(t.Id, t.EventType.ToString(), t.Point!.Name,
                t.Component != null ? t.Component.Name : null, t.ComponentBarcode,
                t.PerformedBy!.EmployeeNo + " " + t.PerformedBy.FirstName + " " + t.PerformedBy.LastName, t.OccurredAt))
            .ToListAsync(ct);

        return new UnitLookupDto(code, true, unit.Product!.Name, status.State.ToString(), status.PointName, status.Since, components, recent);
    }

    // ---- private helpers ----

    /// <summary>Status from a unit's effective rows (the same rule as GetUnitStatusAsync, without another query).</summary>
    private static UnitStatus DeriveStatus(IEnumerable<InventoryTransaction> effectiveRows)
    {
        var last = effectiveRows.LastOrDefault(t => t.EventType is EventType.RECEIVED or EventType.DELIVERED);
        return last?.EventType switch
        {
            EventType.RECEIVED => new UnitStatus(UnitState.InStock, last.Point?.Name, last.OccurredAt),
            EventType.DELIVERED => new UnitStatus(UnitState.Delivered, last.Point?.Name, last.OccurredAt),
            _ => new UnitStatus(UnitState.Registered)
        };
    }

    private static string UserLabel(ApplicationUser u) => $"{u.EmployeeNo} {u.DisplayName}";

    /// <summary>A business-rule error (HTTP 422). With a field name the message also lands under that input in the UI.</summary>
    private static DomainException Fail(string code, string message, string? field = null) =>
        new(code, message) { Errors = field is null ? null : new Dictionary<string, string[]> { [field] = [message] } };

    private static string RequireBarcode(string? raw, string field = "barcode")
    {
        var code = NormalizeBarcode(raw);
        if (!Barcode.IsValid(code))
            throw Fail("invalid-barcode", "Barcode must be 6-32 characters: A-Z, 0-9 and \"-\".", field);
        return code;
    }

    /// <summary>Loads the unit AND locks its row until the transaction ends (SELECT ... FOR UPDATE). Null when unknown.</summary>
    private async Task<Unit?> LockUnitAsync(string code, CancellationToken ct) =>
        (await db.Units.FromSql($"SELECT * FROM units WHERE barcode = {code} FOR UPDATE").ToListAsync(ct)).FirstOrDefault();

    /// <summary>First scan of an unknown barcode (R2): a product is required. The unit is saved together with its first row.</summary>
    private async Task<Unit> NewUnitAsync(string code, long? productId, long userId, CancellationToken ct)
    {
        if (productId is null)
            throw Fail("product-required", $"Unit {code} is new: choose its product.", "productId");
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null || product.IsDeleted)
            throw Fail("product-invalid", "The chosen product does not exist or has been archived.", "productId");

        var unit = new Unit { Barcode = code, ProductId = product.Id, RegisteredAt = clock.UtcNow, RegisteredById = userId };
        db.Units.Add(unit);
        return unit;
    }

    private async Task<Point> RequirePointAsync(long? id, PointType type, string field, CancellationToken ct)
    {
        var point = id is null ? null : await db.Points.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (point is null || point.IsDeleted)
            throw Fail("point-invalid", "The chosen point does not exist or has been archived.", field);
        if (point.PointType != type)
            throw Fail("wrong-point-type", $"\"{point.Name}\" is a {point.PointType} point; a {type} point is required here.", field);
        return point;
    }

    /// <summary>A brand-new (unsaved) unit has no history yet.</summary>
    private async Task<List<InventoryTransaction>> EffectiveRowsOfAsync(Unit unit, CancellationToken ct) =>
        unit.Id == 0 ? [] : await GetEffectiveRowsAsync(unit.Id, ct);

    /// <summary>SaveChanges, turning the one race we expect (two scans registering the same new barcode) into a friendly 422.</summary>
    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw Fail("concurrent-scan", "Someone else just saved this barcode. Scan it again to see its current state.");
        }
    }

    // =====================================================================================================
    // Day 4: admin reversal (rules R7-R10). A reversal NEVER changes the old row: it ADDS a REVERSED row that
    // points at it (reverses_id). "Effective rows" then simply skip both, so status, stock and attached
    // components are re-derived and the unit goes back to what it was before the cancelled step.
    // =====================================================================================================

    /// <summary>The verdict for one row: may it be reversed, why not, what would happen, and which point the REVERSED row records.</summary>
    public record ReversalCheck(bool Allowed, string? BlockedReason, string Effect, long PointId);

    /// <summary>
    /// Pure decision logic (no database access, so it is easy to test): can <paramref name="target"/> be reversed,
    /// given the unit's current effective rows?
    /// Reversal point semantics: COMPONENT_TRACKED and RECEIVED rows record their own point; a reversed DELIVERED row
    /// records the RECEIVE point the unit returns to (R9).
    /// </summary>
    private static ReversalCheck EvaluateReversal(InventoryTransaction target, string unitBarcode, IReadOnlyList<InventoryTransaction> effective)
    {
        ReversalCheck Blocked(string why) => new(false, why, "", target.PointId);

        if (target.EventType == EventType.REVERSED)
            return Blocked("This row is itself a reversal, and a reversal can never be reversed.");
        if (effective.All(e => e.Id != target.Id))
            return Blocked("This row has already been reversed (a row can be reversed only once).");

        switch (target.EventType)
        {
            case EventType.COMPONENT_TRACKED: // R10: detach the component; its barcode becomes free again
                return new(true, null,
                    $"Component {target.Component?.Name} ({target.ComponentBarcode}) will be detached from unit {unitBarcode}. Its barcode can be tracked again.",
                    target.PointId);

            case EventType.RECEIVED: // R8: not once the unit has been delivered
                if (DeriveStatus(effective).State == UnitState.Delivered)
                    return Blocked($"Unit {unitBarcode} has been delivered since this receive. Reverse the delivery first.");
                return new(true, null,
                    $"Unit {unitBarcode} will no longer be in stock. It goes back to Registered (not received).", target.PointId);

            case EventType.DELIVERED: // R9: only the latest effective delivery; the unit returns to its receive point
                var latest = effective.LastOrDefault(e => e.EventType == EventType.DELIVERED);
                var received = effective.LastOrDefault(e => e.EventType == EventType.RECEIVED);
                if (latest is null || latest.Id != target.Id || received is null)
                    return Blocked("Only the latest delivery of a unit can be reversed.");
                return new(true, null,
                    $"Unit {unitBarcode} will return to stock at {received.Point?.Name}.", received.PointId);

            default:
                return Blocked("This kind of row cannot be reversed.");
        }
    }

    /// <summary>The row plus the verdict, for the "are you sure?" screen. Throws NotFoundException for an unknown id.</summary>
    public async Task<(InventoryTransaction Row, ReversalCheck Check)> PreviewReversalAsync(long rowId, CancellationToken ct = default)
    {
        var row = await db.InventoryTransactions.AsNoTracking()
            .Include(t => t.Unit).Include(t => t.Point).Include(t => t.Component)
            .FirstOrDefaultAsync(t => t.Id == rowId, ct)
            ?? throw new NotFoundException($"History row {rowId} does not exist.");
        var effective = await GetEffectiveRowsAsync(row.UnitId, ct);
        return (row, EvaluateReversal(row, row.Unit!.Barcode, effective));
    }

    /// <summary>
    /// Which of these units' rows can be reversed right now? Used to switch the Reverse buttons on or off on the
    /// history page (only asked for admins). Demo scale: one small query per distinct unit on the page.
    /// </summary>
    public async Task<HashSet<long>> ReversibleRowIdsAsync(IEnumerable<string> unitBarcodes, CancellationToken ct = default)
    {
        var allowed = new HashSet<long>();
        foreach (var code in unitBarcodes.Distinct())
        {
            var unit = await db.Units.AsNoTracking().FirstOrDefaultAsync(u => u.Barcode == code, ct);
            if (unit is null) continue;
            var effective = await GetEffectiveRowsAsync(unit.Id, ct);
            foreach (var row in effective)
                if (EvaluateReversal(row, code, effective).Allowed) allowed.Add(row.Id);
        }
        return allowed;
    }

    /// <summary>The answer of a successful reversal.</summary>
    public record ReversalResult(long ReversalId, string UnitBarcode, string ReversedEventType, string Effect);

    /// <summary>
    /// Reverse one history row (admin only; the controller checks the policy). One locked transaction:
    /// reason of 5+ characters, the target must still be reversible, then ONE REVERSED row is added.
    /// </summary>
    public async Task<ReversalResult> ReverseAsync(long rowId, string? reason, long adminId, CancellationToken ct = default)
    {
        var cleanReason = (reason ?? "").Trim();
        if (cleanReason.Length < 5)
            throw Fail("reason-too-short", "Give a reason of at least 5 characters.", "reason");

        var target = await db.InventoryTransactions.AsNoTracking()
            .Include(t => t.Unit).FirstOrDefaultAsync(t => t.Id == rowId, ct)
            ?? throw new NotFoundException($"History row {rowId} does not exist.");
        var code = target.Unit!.Barcode;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockUnitAsync(code, ct); // serialize with scans and other reversals of this unit

        // Re-read AFTER the lock: someone may have changed the unit a moment ago.
        var (row, check) = await PreviewReversalAsync(rowId, ct);
        if (!check.Allowed) throw Fail("reversal-blocked", check.BlockedReason!);

        var reversal = new InventoryTransaction
        {
            UnitId = row.UnitId, EventType = EventType.REVERSED, PointId = check.PointId, ReversesId = row.Id,
            Reason = cleanReason, PerformedById = adminId, OccurredAt = clock.UtcNow,
        };
        db.InventoryTransactions.Add(reversal);
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return new ReversalResult(reversal.Id, code, row.EventType.ToString(), check.Effect);
    }
}
