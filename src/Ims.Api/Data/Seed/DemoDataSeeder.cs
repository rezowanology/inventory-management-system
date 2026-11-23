using Ims.Api.Domain;
using Ims.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Ims.Api.Data.Seed;

/// <summary>
/// Deterministic demo data for the presentation (<c>dotnet run --project src/Ims.Api -- seed --demo-data</c>).
/// About 25 units DEMO000001... spread over the last 30 days. Everything goes through <see cref="InventoryService"/>,
/// so every rule applies exactly as for real scans (including the reversals). The sample record of the brief is never touched.
/// Same seed number (42) -> same products, points and counts every time; the dates are relative to "now".
/// </summary>
public static class DemoDataSeeder
{
    /// <summary>A clock we can set, so the demo rows can be written with dates in the past.</summary>
    private sealed class DemoClock : IClock { public DateTime UtcNow { get; set; } }

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ImsDbContext>();
        if (await db.Units.AnyAsync(u => u.Barcode == "DEMO000001")) { Console.WriteLine("Demo data already present."); return; }

        var time = services.GetRequiredService<AppTime>();
        var clock = new DemoClock();
        var inventory = new InventoryService(db, clock);   // the real rules, with a clock we control
        var master = new MasterDataService(db);
        var rnd = new Random(42);

        // ---- who and what ----
        var tracker = await db.Users.SingleAsync(u => u.EmployeeNo == "66798");
        var receiver = await db.Users.SingleAsync(u => u.EmployeeNo == "58877");
        var deliverer = await db.Users.SingleAsync(u => u.EmployeeNo == "54671");
        var admin = await db.Users.SingleAsync(u => u.EmployeeNo == "10001");
        var products = await db.Products.Where(p => !p.IsDeleted).OrderBy(p => p.Id).ToListAsync();
        var trackPoint = await db.Points.SingleAsync(p => p.Name == "Refrigerator Component Track Point 1");
        var receivePoints = await db.Points.Where(p => p.PointType == PointType.RECEIVE && !p.IsDeleted).OrderBy(p => p.Id).ToListAsync();
        var deliveryPoints = await db.Points.Where(p => p.PointType == PointType.DELIVERY && !p.IsDeleted).OrderBy(p => p.Id).ToListAsync();

        // One extra component that will be archived at the end (it is used, so deleting it soft-deletes it, rule R11).
        var fanMotor = await db.Components.FirstOrDefaultAsync(c => c.Name == "Fan Motor");
        if (fanMotor is null) { fanMotor = new Component { Name = "Fan Motor" }; db.Components.Add(fanMotor); await db.SaveChangesAsync(); }
        var components = await db.Components.Where(c => c.Name != "Fan Motor").OrderBy(c => c.Id).ToListAsync();

        var now = DateTime.UtcNow;
        var dayStart = time.StartOfDayUtc(time.LocalDay(now));
        var elapsedToday = now - dayStart; // how much of today has passed (so "today" rows are never in the future)
        DateTime Ago(int days) => now.AddDays(-days).AddMinutes(-rnd.Next(0, 600));
        DateTime Today(double fraction) => dayStart + elapsedToday * fraction;

        string Code(int n) => $"DEMO{n:D6}";

        async Task TrackAsync(int n, DateTime at, int componentCount, bool useFanMotor = false)
        {
            clock.UtcNow = at;
            var items = new List<InventoryService.TrackItem>();
            for (var k = 1; k <= componentCount; k++)
            {
                var comp = useFanMotor && k == 1 ? fanMotor! : components[rnd.Next(components.Count)];
                items.Add(new InventoryService.TrackItem(comp.Id, $"DC{n:D6}{k}"));
            }
            await inventory.TrackAsync(Code(n), products[rnd.Next(products.Count)].Id, trackPoint.Id, items, tracker.Id);
        }
        async Task ReceiveAsync(int n, DateTime at)
        {
            clock.UtcNow = at;
            await inventory.ReceiveAsync(Code(n), null, receivePoints[rnd.Next(receivePoints.Count)].Id, receiver.Id);
        }
        async Task DeliverAsync(int n, DateTime at)
        {
            clock.UtcNow = at;
            await inventory.DeliverAsync(Code(n), deliveryPoints[rnd.Next(deliveryPoints.Count)].Id, deliverer.Id);
        }
        async Task<long> RowIdAsync(int n, EventType type)
        {
            var barcode = Code(n); // computed first: a LINQ query cannot call a local function
            return await db.InventoryTransactions.Where(t => t.Unit!.Barcode == barcode && t.EventType == type)
                .OrderBy(t => t.Id).Select(t => t.Id).FirstAsync();
        }

        // ---- 1-10: in stock for 20+ days (they show up in "stuck in stock the longest") ----
        for (var n = 1; n <= 10; n++)
        {
            var received = Ago(rnd.Next(20, 29));
            await TrackAsync(n, received.AddDays(-1), rnd.Next(1, 4), useFanMotor: n == 4);
            await ReceiveAsync(n, received);
        }
        // ---- 11-16: in stock for a few days ----
        for (var n = 11; n <= 16; n++)
        {
            var received = Ago(rnd.Next(1, 15));
            await TrackAsync(n, received.AddDays(-1), rnd.Next(1, 3));
            await ReceiveAsync(n, received);
        }
        // ---- 17-21: already delivered ----
        for (var n = 17; n <= 21; n++)
        {
            await TrackAsync(n, Ago(rnd.Next(26, 29)), rnd.Next(1, 3));
            await ReceiveAsync(n, Ago(rnd.Next(12, 25)));
            await DeliverAsync(n, Ago(rnd.Next(1, 10)));
        }
        // ---- 22-24: today ----
        await TrackAsync(22, Ago(1), 2); await ReceiveAsync(22, Today(0.30));
        await TrackAsync(23, Ago(1), 1); await ReceiveAsync(23, Today(0.40));
        await TrackAsync(24, Ago(1), 2); await ReceiveAsync(24, Today(0.20)); await DeliverAsync(24, Today(0.60));
        // ---- 25: only tracked so far (Registered) ----
        await TrackAsync(25, Ago(2), 2);

        // ---- one reversal of each type, by the admin, through the real rules ----
        clock.UtcNow = Today(0.70);
        await inventory.ReverseAsync(await RowIdAsync(25, EventType.COMPONENT_TRACKED), "Demo: component scanned on the wrong unit", admin.Id);
        clock.UtcNow = Today(0.80);
        await inventory.ReverseAsync(await RowIdAsync(23, EventType.RECEIVED), "Demo: received at the wrong dock", admin.Id);
        clock.UtcNow = Today(0.90);
        await inventory.ReverseAsync(await RowIdAsync(21, EventType.DELIVERED), "Demo: delivery was cancelled by the customer", admin.Id);

        // ---- one archived master-data item: "Fan Motor" is attached to DEMO000004, so this archives instead of deleting ----
        await master.DeleteAsync<Component>(fanMotor!.Id, CancellationToken.None);

        Console.WriteLine("Demo data added: units DEMO000001-DEMO000025, 3 reversals, 1 archived component (Fan Motor).");
    }
}
