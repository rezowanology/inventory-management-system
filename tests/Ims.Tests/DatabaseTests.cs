using Ims.Api.Data;
using Ims.Api.Data.Seed;
using Ims.Api.Domain;
using Ims.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ims.Tests;

/// <summary>Append-only enforcement (EF interceptor + PostgreSQL trigger), the seed, and derived status.</summary>
[Collection(ApiCollection.Name)]
public class DatabaseTests(ImsApiFactory factory)
{
    private AsyncServiceScope Scope() => factory.Services.CreateAsyncScope();

    [Fact]
    public async Task Barcode_pattern_is_pinned_and_matches_the_client()
    {
        // The React side has the identical test (client/src/lib/barcode.test.ts).
        Assert.Equal("^[A-Z0-9-]{6,32}$", Barcode.Pattern);
        Assert.Equal("2440701AK2057", InventoryService.NormalizeBarcode("  2440701ak2057 "));
        Assert.False(Barcode.IsValid("ABC12"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Ef_refuses_to_update_or_delete_history()
    {
        await using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();

        var row = await db.InventoryTransactions.FirstAsync();
        row.Reason = "tampered";
        var update = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("append-only", update.Message);

        db.ChangeTracker.Clear();
        var row2 = await db.InventoryTransactions.FirstAsync();
        db.InventoryTransactions.Remove(row2);
        var delete = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("append-only", delete.Message);
    }

    [Fact]
    public async Task Postgres_trigger_refuses_raw_sql_update_and_delete()
    {
        await using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();

        var upd = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE inventory_transactions SET reason = 'x'"));
        Assert.Contains("append-only", upd.ToString());

        var del = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM inventory_transactions"));
        Assert.Contains("append-only", del.ToString());

        // audit_log is protected too. It needs a row first (a row-level trigger only fires on existing rows).
        db.AuditLogs.Add(new AuditLog { Action = "TEST", EntityType = "Test", EntityId = "1", EntityRepr = "trigger test" });
        await db.SaveChangesAsync();
        var audit = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE audit_log SET action = 'x'"));
        Assert.Contains("append-only", audit.ToString());
    }

    [Fact]
    public async Task Check_constraint_rejects_a_reversal_without_a_reason()
    {
        await using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();
        var target = await db.InventoryTransactions.AsNoTracking().FirstAsync(t => t.EventType == EventType.DELIVERED);

        db.InventoryTransactions.Add(new InventoryTransaction
        {
            UnitId = target.UnitId, EventType = EventType.REVERSED, PointId = target.PointId,
            ReversesId = target.Id, Reason = "no", PerformedById = target.PerformedById, OccurredAt = DateTime.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Seeding_twice_keeps_exactly_the_three_sample_rows_and_the_unit_is_delivered()
    {
        await using var scope = Scope();
        await DbSeeder.SeedAsync(scope.ServiceProvider); // second (or third) run: must add nothing
        var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();

        var rows = await db.InventoryTransactions.AsNoTracking()
            .Where(t => t.Unit!.Barcode == DbSeeder.SampleBarcode).OrderBy(t => t.OccurredAt).ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.Equal([EventType.COMPONENT_TRACKED, EventType.RECEIVED, EventType.DELIVERED], rows.Select(r => r.EventType));
        Assert.Equal(1, await db.Units.CountAsync(u => u.Barcode == DbSeeder.SampleBarcode));
        Assert.Equal(5, await db.Users.CountAsync());

        // 10:00 in Dhaka (UTC+6) is 04:00 UTC (A12).
        Assert.Equal(new DateTime(2026, 3, 27, 4, 0, 0, DateTimeKind.Utc), rows[0].OccurredAt);
        Assert.Equal("94A600321852", rows[0].ComponentBarcode);

        var inventory = scope.ServiceProvider.GetRequiredService<InventoryService>();
        var status = await inventory.GetUnitStatusAsync("2440701ak2057");
        Assert.Equal(UnitState.Delivered, status.State);
        Assert.Equal("Refrigerator Delivery Point 1", status.PointName);
        Assert.Single(await inventory.GetAttachedComponentsAsync(DbSeeder.SampleBarcode));
        Assert.Equal(UnitState.NotFound, (await inventory.GetUnitStatusAsync("NOSUCHUNIT1")).State);
    }
}
