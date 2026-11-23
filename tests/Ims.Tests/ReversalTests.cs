using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ims.Api.Data;
using Ims.Api.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Ims.Tests;

/// <summary>Day 4: admin reversals (rules R7-R10). Every test builds its own unit, so the seeded sample record stays untouched.</summary>
[Collection(ApiCollection.Name)]
public class ReversalTests(ImsApiFactory factory)
{
    private async Task<HttpClient> SignInAsync(string user)
    {
        var client = await ApiTestHelpers.CreateClientAsync(factory);
        (await ApiTestHelpers.LoginAsync(client, user, user == "admin" ? "Admin@12345" : "Demo@12345")).EnsureSuccessStatusCode();
        return client;
    }

    private static string NewBarcode() => "R" + Guid.NewGuid().ToString("N")[..11].ToUpperInvariant();

    private long IdOf<T>(string name) where T : MasterDataEntity
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ImsDbContext>().Set<T>().Where(x => x.Name == name).Select(x => x.Id).First();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>A unit that was tracked (one component), received at <paramref name="receivePoint"/> and delivered. Returns the barcodes and the id of each row.</summary>
    private async Task<(string Unit, string Part, long TrackedId, long ReceivedId, long DeliveredId)> DeliveredUnitAsync(
        string receivePoint = "Refrigerator Receive Point 2")
    {
        var tracker = await SignInAsync("tracker");
        var receiver = await SignInAsync("receiver");
        var deliverer = await SignInAsync("deliverer");
        var unit = NewBarcode();
        var part = NewBarcode();

        (await tracker.PostAsJsonAsync("/api/ops/track", new
        {
            barcode = unit, productId = IdOf<Product>("Refrigerator"), trackPointId = IdOf<Point>("Refrigerator Component Track Point 1"),
            components = new[] { new { componentId = IdOf<Component>("Compressor"), componentBarcode = part } },
        })).EnsureSuccessStatusCode();
        (await receiver.PostAsJsonAsync("/api/ops/receive", new { barcode = unit, receivePointId = IdOf<Point>(receivePoint) })).EnsureSuccessStatusCode();
        (await deliverer.PostAsJsonAsync("/api/ops/deliver", new { barcode = unit, deliveryPointId = IdOf<Point>("Refrigerator Delivery Point 1") })).EnsureSuccessStatusCode();

        var admin = await SignInAsync("admin");
        var rows = (await Json(await admin.GetAsync($"/api/history?q={unit}&sort=time"))).GetProperty("items").EnumerateArray().ToList();
        long Id(string ev) => rows.First(r => r.GetProperty("eventType").GetString() == ev).GetProperty("id").GetInt64();
        return (unit, part, Id("COMPONENT_TRACKED"), Id("RECEIVED"), Id("DELIVERED"));
    }

    private static Task<HttpResponseMessage> Reverse(HttpClient c, long id, string reason = "Scanned by mistake") =>
        c.PostAsJsonAsync($"/api/history/{id}/reverse", new { reason });

    private static async Task<JsonElement> LookupAsync(HttpClient c, string barcode) =>
        await Json(await c.GetAsync($"/api/units/lookup?barcode={barcode}"));

    // ---- R9: reversing a delivery ----

    [Fact]
    public async Task Reversing_a_delivery_puts_the_unit_back_in_stock_at_its_ORIGINAL_receive_point()
    {
        var (unit, _, _, _, deliveredId) = await DeliveredUnitAsync("Refrigerator Receive Point 2");
        var admin = await SignInAsync("admin");
        var stockBefore = (await Json(await admin.GetAsync("/api/dashboard"))).GetProperty("stockCount").GetInt32();

        var response = await Reverse(admin, deliveredId, "Delivered to the wrong customer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await LookupAsync(admin, unit);
        Assert.Equal("InStock", state.GetProperty("status").GetString());
        Assert.Equal("Refrigerator Receive Point 2", state.GetProperty("currentPoint").GetString()); // not Point 1: the point it came from
        Assert.Equal(stockBefore + 1, (await Json(await admin.GetAsync("/api/dashboard"))).GetProperty("stockCount").GetInt32());

        // The stock report derives from history, so the unit shows up there too.
        var stock = await Json(await admin.GetAsync($"/api/reports/stock?q={unit}"));
        Assert.Equal(1, stock.GetProperty("totalCount").GetInt32());
        Assert.Equal("Refrigerator Receive Point 2", stock.GetProperty("items")[0].GetProperty("pointName").GetString());
    }

    [Fact]
    public async Task A_reversal_adds_a_REVERSED_row_and_leaves_the_old_row_untouched()
    {
        var (unit, _, _, _, deliveredId) = await DeliveredUnitAsync();
        var admin = await SignInAsync("admin");

        var result = await Json(await Reverse(admin, deliveredId, "Wrong customer"));
        Assert.Equal("DELIVERED", result.GetProperty("reversedEventType").GetString());

        var rows = (await Json(await admin.GetAsync($"/api/history?q={unit}&sort=time"))).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(4, rows.Count); // tracked, received, delivered + the new REVERSED row
        var delivered = rows.Single(r => r.GetProperty("id").GetInt64() == deliveredId);
        Assert.True(delivered.GetProperty("reversed").GetBoolean());
        Assert.StartsWith("10001", delivered.GetProperty("reversedBy").GetString()); // "<employee no.> <name>" of the admin
        var reversed = rows.Single(r => r.GetProperty("eventType").GetString() == "REVERSED");
        Assert.Equal(deliveredId, reversed.GetProperty("reversesId").GetInt64());
        Assert.Equal("Wrong customer", reversed.GetProperty("reason").GetString());
    }

    // ---- R8: reversing a receive ----

    [Fact]
    public async Task Reversing_a_receive_while_the_unit_is_delivered_is_blocked_with_an_explanation()
    {
        var (unit, _, _, receivedId, deliveredId) = await DeliveredUnitAsync();
        var admin = await SignInAsync("admin");

        var blocked = await Reverse(admin, receivedId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        var problem = await Json(blocked);
        Assert.Equal("reversal-blocked", problem.GetProperty("code").GetString());
        Assert.Contains("Reverse the delivery first", problem.GetProperty("detail").GetString());
        Assert.Equal("Delivered", (await LookupAsync(admin, unit)).GetProperty("status").GetString()); // nothing changed

        // The preview explains it too, without writing anything.
        var preview = await Json(await admin.GetAsync($"/api/history/{receivedId}/reverse-preview"));
        Assert.False(preview.GetProperty("allowed").GetBoolean());
        Assert.Contains("delivered", preview.GetProperty("blockedReason").GetString());

        // Reverse the delivery first, then the receive works and the unit is only Registered again.
        (await Reverse(admin, deliveredId)).EnsureSuccessStatusCode();
        (await Reverse(admin, receivedId, "Received at the wrong dock")).EnsureSuccessStatusCode();
        Assert.Equal("Registered", (await LookupAsync(admin, unit)).GetProperty("status").GetString());
    }

    // ---- R10: reversing a component ----

    [Fact]
    public async Task Reversing_a_component_detaches_it_and_its_barcode_can_be_tracked_on_another_unit()
    {
        var (unit, part, trackedId, _, _) = await DeliveredUnitAsync();
        var admin = await SignInAsync("admin");
        var tracker = await SignInAsync("tracker");
        object TrackOnto(string barcode) => new
        {
            barcode, productId = IdOf<Product>("Refrigerator"), trackPointId = IdOf<Point>("Refrigerator Component Track Point 1"),
            components = new[] { new { componentId = IdOf<Component>("Compressor"), componentBarcode = part } },
        };

        // Attached to the first unit, so a second unit is refused.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await tracker.PostAsJsonAsync("/api/ops/track", TrackOnto(NewBarcode()))).StatusCode);

        (await Reverse(admin, trackedId, "Component belongs elsewhere")).EnsureSuccessStatusCode();

        Assert.Empty((await LookupAsync(admin, unit)).GetProperty("components").EnumerateArray());
        var second = NewBarcode();
        Assert.Equal(HttpStatusCode.OK, (await tracker.PostAsJsonAsync("/api/ops/track", TrackOnto(second))).StatusCode);

        // The barcode report still knows BOTH units for that component barcode (history is complete): the user picks one.
        var report = await Json(await admin.GetAsync($"/api/reports/barcode?barcode={part}"));
        Assert.True(report.GetProperty("found").GetBoolean());
        Assert.Equal(2, report.GetProperty("matches").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, report.GetProperty("unit").ValueKind);
    }

    // ---- R7: what cannot be reversed ----

    [Fact]
    public async Task A_row_cannot_be_reversed_twice_and_a_REVERSED_row_cannot_be_reversed()
    {
        var (unit, _, trackedId, _, _) = await DeliveredUnitAsync();
        var admin = await SignInAsync("admin");
        (await Reverse(admin, trackedId)).EnsureSuccessStatusCode();

        var again = await Reverse(admin, trackedId);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
        Assert.Contains("already been reversed", (await Json(again)).GetProperty("detail").GetString());

        var reversalRow = (await Json(await admin.GetAsync($"/api/history?q={unit}"))).GetProperty("items").EnumerateArray()
            .First(r => r.GetProperty("eventType").GetString() == "REVERSED");
        Assert.False(reversalRow.GetProperty("canReverse").GetBoolean()); // no Reverse button
        var onReversal = await Reverse(admin, reversalRow.GetProperty("id").GetInt64());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, onReversal.StatusCode);
        Assert.Contains("never be reversed", (await Json(onReversal)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Only_the_latest_effective_delivery_row_is_reversible_and_unknown_rows_are_404()
    {
        var (_, _, trackedId, _, deliveredId) = await DeliveredUnitAsync();
        var admin = await SignInAsync("admin");

        // canReverse flags on the list: tracked + delivered yes, received no (the unit is delivered).
        var unit = (await Json(await admin.GetAsync($"/api/history/{deliveredId}/reverse-preview"))).GetProperty("row").GetProperty("unitBarcode").GetString();
        var rows = (await Json(await admin.GetAsync($"/api/history?q={unit}"))).GetProperty("items").EnumerateArray().ToList();
        bool Can(string ev) => rows.Single(r => r.GetProperty("eventType").GetString() == ev).GetProperty("canReverse").GetBoolean();
        Assert.True(Can("COMPONENT_TRACKED"));
        Assert.False(Can("RECEIVED"));
        Assert.True(Can("DELIVERED"));
        Assert.True((await Json(await admin.GetAsync($"/api/history/{trackedId}/reverse-preview"))).GetProperty("allowed").GetBoolean());

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/history/999999999/reverse-preview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Reverse(admin, 999999999)).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("    a   ")]
    public async Task A_reason_shorter_than_5_characters_is_refused_and_nothing_is_saved(string reason)
    {
        var (unit, _, _, _, deliveredId) = await DeliveredUnitAsync();
        var admin = await SignInAsync("admin");

        var response = await Reverse(admin, deliveredId, reason);

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity);
        Assert.Equal("Delivered", (await LookupAsync(admin, unit)).GetProperty("status").GetString());
    }

    // ---- R1: who may reverse ----

    [Theory]
    [InlineData("receiver")]
    [InlineData("deliverer")]
    [InlineData("tracker")]
    [InlineData("viewer")]
    public async Task Only_an_admin_may_reverse_everybody_else_gets_403(string user)
    {
        var (_, _, _, _, deliveredId) = await DeliveredUnitAsync();
        var client = await SignInAsync(user);

        Assert.Equal(HttpStatusCode.Forbidden, (await Reverse(client, deliveredId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/history/{deliveredId}/reverse-preview")).StatusCode);
    }

    [Fact]
    public async Task The_report_viewer_sees_history_but_never_gets_canReverse()
    {
        var (unit, _, _, _, _) = await DeliveredUnitAsync();
        var viewer = await SignInAsync("viewer");

        var rows = (await Json(await viewer.GetAsync($"/api/history?q={unit}"))).GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.False(r.GetProperty("canReverse").GetBoolean()));
    }

    [Fact]
    public async Task The_database_trigger_still_refuses_to_reverse_a_REVERSED_row_directly()
    {
        // Safety net: even code that bypasses InventoryService cannot reverse a reversal.
        var (unit, _, trackedId, _, _) = await DeliveredUnitAsync();
        var admin = await SignInAsync("admin");
        (await Reverse(admin, trackedId)).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();
        var reversal = db.InventoryTransactions.First(t => t.ReversesId == trackedId);
        db.InventoryTransactions.Add(new InventoryTransaction
        {
            UnitId = reversal.UnitId, EventType = EventType.REVERSED, PointId = reversal.PointId, ReversesId = reversal.Id,
            Reason = "trying to cancel a cancel", PerformedById = reversal.PerformedById, OccurredAt = DateTime.UtcNow,
        });
        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        Assert.NotNull(unit);
    }
}
