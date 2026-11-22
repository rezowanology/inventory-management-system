using Ims.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ims.Api.Data.Seed;

/// <summary>
/// Seeds demo users, master data and the exact 3-row sample record from the assignment brief.
/// Idempotent: running it twice adds nothing the second time. Run with: dotnet run --project src/Ims.Api -- seed
/// (after "dotnet ef database update"). The passwords below are DEMO-ONLY values required by the README.
/// </summary>
public static class DbSeeder
{
    public const string SampleBarcode = "2440701AK2057";

    private record SeedUser(string UserName, string First, string Last, Role Role, string EmployeeNo, string Password);

    private static readonly SeedUser[] Users =
    [
        new("admin", "System", "Admin", Role.ADMIN, "10001", "Admin@12345"),
        new("tracker", "Tina", "Tracker", Role.COMPONENT_TRACKER, "66798", "Demo@12345"),
        new("receiver", "Rafi", "Receiver", Role.RECEIVE_USER, "58877", "Demo@12345"),
        new("deliverer", "Dina", "Deliverer", Role.DELIVERY_USER, "54671", "Demo@12345"),
        new("viewer", "Vera", "Viewer", Role.REPORT_VIEWER, "20001", "Demo@12345"),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ImsDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var tzId = services.GetRequiredService<IConfiguration>()["App:TimeZone"] ?? "UTC";
        var zone = FindZone(tzId);

        // 1) users, through UserManager so the password is hashed
        foreach (var s in Users)
        {
            if (await userManager.FindByNameAsync(s.UserName) is not null) continue;
            var result = await userManager.CreateAsync(new ApplicationUser
            {
                UserName = s.UserName, FirstName = s.First, LastName = s.Last,
                Role = s.Role, EmployeeNo = s.EmployeeNo, IsActive = true
            }, s.Password);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Seeding user '{s.UserName}' failed: " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        // 2) master data (add only what is missing, matched by name)
        foreach (var n in new[] { "Refrigerator", "Freezer", "Air Conditioner" })
            if (!await db.Products.AnyAsync(p => p.Name == n)) db.Products.Add(new Product { Name = n });
        foreach (var n in new[] { "Compressor", "Condenser", "Evaporator", "Thermostat" })
            if (!await db.Components.AnyAsync(c => c.Name == n)) db.Components.Add(new Component { Name = n });
        foreach (var (name, type) in new[]
        {
            ("Refrigerator Component Track Point 1", PointType.TRACK),
            ("Refrigerator Receive Point 1", PointType.RECEIVE),
            ("Refrigerator Receive Point 2", PointType.RECEIVE),
            ("Refrigerator Delivery Point 1", PointType.DELIVERY),
            ("Refrigerator Delivery Point 2", PointType.DELIVERY),
        })
            if (!await db.Points.AnyAsync(p => p.Name == name && p.PointType == type))
                db.Points.Add(new Point { Name = name, PointType = type });
        await db.SaveChangesAsync();

        // 3) the sample record, exactly as in the brief, in this order
        if (await db.Units.AnyAsync(u => u.Barcode == SampleBarcode)) return;

        var tracker = await db.Users.SingleAsync(u => u.EmployeeNo == "66798");
        var receiver = await db.Users.SingleAsync(u => u.EmployeeNo == "58877");
        var deliverer = await db.Users.SingleAsync(u => u.EmployeeNo == "54671");
        var fridge = await db.Products.SingleAsync(p => p.Name == "Refrigerator");
        var compressor = await db.Components.SingleAsync(c => c.Name == "Compressor");
        var trackPoint = await db.Points.SingleAsync(p => p.Name == "Refrigerator Component Track Point 1");
        var receivePoint = await db.Points.SingleAsync(p => p.Name == "Refrigerator Receive Point 1");
        var deliverPoint = await db.Points.SingleAsync(p => p.Name == "Refrigerator Delivery Point 1");

        // The brief's times are LOCAL times in App:TimeZone; the database stores UTC (A12).
        DateTime Utc(int y, int mo, int d, int h, int mi) =>
            TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified), zone);

        await using var tx = await db.Database.BeginTransactionAsync();
        var unit = new Unit
        {
            Barcode = SampleBarcode, ProductId = fridge.Id,
            RegisteredAt = Utc(2026, 3, 27, 10, 0), RegisteredById = tracker.Id
        };
        db.Units.Add(unit);
        db.InventoryTransactions.AddRange(
            new InventoryTransaction
            {
                Unit = unit, EventType = EventType.COMPONENT_TRACKED, PointId = trackPoint.Id,
                ComponentId = compressor.Id, ComponentBarcode = "94A600321852",
                PerformedById = tracker.Id, OccurredAt = Utc(2026, 3, 27, 10, 0)
            },
            new InventoryTransaction
            {
                Unit = unit, EventType = EventType.RECEIVED, PointId = receivePoint.Id,
                PerformedById = receiver.Id, OccurredAt = Utc(2026, 3, 28, 10, 15)
            },
            new InventoryTransaction
            {
                Unit = unit, EventType = EventType.DELIVERED, PointId = deliverPoint.Id,
                PerformedById = deliverer.Id, OccurredAt = Utc(2026, 3, 30, 11, 30)
            });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>"Asia/Dhaka" (IANA) works on Windows with .NET 6+ when ICU is available; fall back to the Windows id.</summary>
    private static TimeZoneInfo FindZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) when (id == "Asia/Dhaka")
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Bangladesh Standard Time");
        }
    }
}
