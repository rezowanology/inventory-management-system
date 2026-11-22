using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ims.Api.Data;
using Ims.Api.Domain;
using Ims.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ims.Tests;

/// <summary>Day 3: track / receive / deliver rules R2-R5, the DB safety-net trigger, lookup and the history filters.</summary>
[Collection(ApiCollection.Name)]
public class InventoryTests(ImsApiFactory factory)
{
    private const string SeededUnit = "2440701AK2057";
    private const string SeededComponentBarcode = "94A600321852";

    // ---- helpers ----

    /// <summary>Signs in with the real login (cookie), so performed_by is a real user row.</summary>
    private async Task<HttpClient> SignInAsync(string user)
    {
        var client = await ApiTestHelpers.CreateClientAsync(factory);
        var response = await ApiTestHelpers.LoginAsync(client, user, user == "admin" ? "Admin@12345" : "Demo@12345");
        response.EnsureSuccessStatusCode();
        return client;
    }

    private static string NewBarcode() => "T" + Guid.NewGuid().ToString("N")[..11].ToUpperInvariant();

    private long IdOf<T>(string name) where T : MasterDataEntity
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ImsDbContext>().Set<T>().Where(x => x.Name == name).Select(x => x.Id).First();
    }

    private long Fridge => IdOf<Product>("Refrigerator");
    private long TrackPoint => IdOf<Point>("Refrigerator Component Track Point 1");
    private long ReceivePoint => IdOf<Point>("Refrigerator Receive Point 1");
    private long DeliveryPoint => IdOf<Point>("Refrigerator Delivery Point 1");

    private static Task<HttpResponseMessage> Post(HttpClient c, string url, object body) => c.PostAsJsonAsync(url, body);

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private object TrackBody(string barcode, params (string component, string code)[] parts) => new
    {
        barcode, productId = Fridge, trackPointId = TrackPoint,
        components = parts.Select(p => new { componentId = IdOf<Component>(p.component), componentBarcode = p.code }).ToArray(),
    };

    private async Task<JsonElement> LookupAsync(HttpClient c, string barcode) =>
        await Json(await c.GetAsync($"/api/units/lookup?barcode={barcode}"));

    private async Task<int> StockAsync(HttpClient c) => (await Json(await c.GetAsync("/api/dashboard"))).GetProperty("stockCount").GetInt32();

    // ---- R2 / R3: tracking ----

    [Fact]
    public async Task Track_first_scan_registers_the_unit_and_writes_one_row_per_component()
    {
        var tracker = await SignInAsync("tracker");
        var code = NewBarcode();

        var response = await Post(tracker, "/api/ops/track", TrackBody(code, ("Compressor", NewBarcode()), ("Thermostat", NewBarcode())));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var unit = await Json(response); // the answer already contains the updated unit
        Assert.True(unit.GetProperty("exists").GetBoolean());
        Assert.Equal("Registered", unit.GetProperty("status").GetString());
        Assert.Equal("Refrigerator", unit.GetProperty("product").GetString());
        Assert.Equal(2, unit.GetProperty("components").GetArrayLength());
        Assert.Equal(2, unit.GetProperty("recentEvents").GetArrayLength());
    }

    [Fact]
    public async Task Track_unknown_barcode_without_a_product_is_refused()
    {
        var tracker = await SignInAsync("tracker");
        var code = NewBarcode();
        var body = new { barcode = code, trackPointId = TrackPoint, components = new[] { new { componentId = IdOf<Component>("Compressor"), componentBarcode = NewBarcode() } } };

        var response = await Post(tracker, "/api/ops/track", body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await Json(response);
        Assert.Equal("product-required", problem.GetProperty("code").GetString());
        Assert.False((await LookupAsync(tracker, code)).GetProperty("exists").GetBoolean());
    }

    [Fact]
    public async Task Track_component_attached_to_another_unit_is_a_422_naming_that_unit()
    {
        var tracker = await SignInAsync("tracker");

        var response = await Post(tracker, "/api/ops/track", TrackBody(NewBarcode(), ("Condenser", SeededComponentBarcode)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await Json(response);
        Assert.Contains($"already attached to unit {SeededUnit}", problem.GetProperty("detail").GetString());
        // The error is also placed on the matching row of the batch.
        Assert.True(problem.GetProperty("errors").TryGetProperty("components[0].componentBarcode", out _));
    }

    [Fact]
    public async Task Track_component_already_on_this_unit_is_a_409_duplicate_scan_warning_and_saves_nothing()
    {
        var tracker = await SignInAsync("tracker");
        var code = NewBarcode();
        var part = NewBarcode();
        (await Post(tracker, "/api/ops/track", TrackBody(code, ("Compressor", part)))).EnsureSuccessStatusCode();

        var again = await Post(tracker, "/api/ops/track", TrackBody(code, ("Compressor", part)));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var warning = await Json(again);
        Assert.Equal("duplicate-scan", warning.GetProperty("type").GetString());
        Assert.Equal("Refrigerator Component Track Point 1", warning.GetProperty("where").GetString());
        Assert.Contains("66798", warning.GetProperty("by").GetString());
        Assert.Equal(1, (await LookupAsync(tracker, code)).GetProperty("components").GetArrayLength());
    }

    [Fact]
    public async Task Track_batch_is_all_or_nothing()
    {
        var tracker = await SignInAsync("tracker");
        var code = NewBarcode();

        // First component is fine, second one is taken by another unit: nothing may be written, not even the unit.
        var response = await Post(tracker, "/api/ops/track",
            TrackBody(code, ("Compressor", NewBarcode()), ("Evaporator", SeededComponentBarcode)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.False((await LookupAsync(tracker, code)).GetProperty("exists").GetBoolean());
    }

    [Fact]
    public async Task Track_component_barcode_equal_to_the_unit_barcode_or_repeated_in_the_batch_is_refused()
    {
        var tracker = await SignInAsync("tracker");
        var code = NewBarcode();
        var same = NewBarcode();

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await Post(tracker, "/api/ops/track", TrackBody(code, ("Compressor", code)))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await Post(tracker, "/api/ops/track", TrackBody(code, ("Compressor", same), ("Condenser", same)))).StatusCode);
    }

    [Fact]
    public async Task Track_is_blocked_after_delivery()
    {
        var tracker = await SignInAsync("tracker");

        var response = await Post(tracker, "/api/ops/track", TrackBody(SeededUnit, ("Compressor", NewBarcode())));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("unit-delivered", (await Json(response)).GetProperty("code").GetString());
    }

    // ---- R2 / R4: receiving ----

    [Fact]
    public async Task Receive_first_scan_needs_a_product_then_puts_the_unit_in_stock()
    {
        var receiver = await SignInAsync("receiver");
        var code = NewBarcode();

        var noProduct = await Post(receiver, "/api/ops/receive", new { barcode = code, receivePointId = ReceivePoint });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noProduct.StatusCode);

        var before = await StockAsync(receiver);
        var ok = await Post(receiver, "/api/ops/receive", new { barcode = code, productId = Fridge, receivePointId = ReceivePoint });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var unit = await Json(ok);
        Assert.Equal("InStock", unit.GetProperty("status").GetString());
        Assert.Equal("Refrigerator Receive Point 1", unit.GetProperty("currentPoint").GetString());
        Assert.Equal(before + 1, await StockAsync(receiver));
    }

    [Fact]
    public async Task Receive_an_unit_already_in_stock_is_a_409_warning_with_when_where_by_and_saves_nothing()
    {
        var receiver = await SignInAsync("receiver");
        var code = NewBarcode();
        (await Post(receiver, "/api/ops/receive", new { barcode = code, productId = Fridge, receivePointId = ReceivePoint })).EnsureSuccessStatusCode();

        var again = await Post(receiver, "/api/ops/receive", new { barcode = code, receivePointId = IdOf<Point>("Refrigerator Receive Point 2") });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var warning = await Json(again);
        Assert.Equal("duplicate-scan", warning.GetProperty("type").GetString());
        Assert.Equal("Refrigerator Receive Point 1", warning.GetProperty("where").GetString()); // where it already is
        Assert.Contains("58877", warning.GetProperty("by").GetString());
        Assert.True(warning.TryGetProperty("when", out _));
        Assert.Equal(1, (await LookupAsync(receiver, code)).GetProperty("recentEvents").GetArrayLength());
    }

    [Fact]
    public async Task Receive_a_delivered_unit_is_a_422()
    {
        var receiver = await SignInAsync("receiver");

        var response = await Post(receiver, "/api/ops/receive", new { barcode = SeededUnit, receivePointId = ReceivePoint });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("unit-delivered", (await Json(response)).GetProperty("code").GetString());
    }

    // ---- R5: delivering ----

    [Fact]
    public async Task Deliver_needs_the_unit_to_be_in_stock_and_then_lowers_the_stock_count()
    {
        var receiver = await SignInAsync("receiver");
        var deliverer = await SignInAsync("deliverer");
        var code = NewBarcode();

        // Registered (tracked only) -> not in stock yet.
        (await Post(await SignInAsync("tracker"), "/api/ops/track", TrackBody(code, ("Compressor", NewBarcode())))).EnsureSuccessStatusCode();
        var early = await Post(deliverer, "/api/ops/deliver", new { barcode = code, deliveryPointId = DeliveryPoint });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, early.StatusCode);
        Assert.Equal("not-in-stock", (await Json(early)).GetProperty("code").GetString());

        (await Post(receiver, "/api/ops/receive", new { barcode = code, receivePointId = ReceivePoint })).EnsureSuccessStatusCode();
        var inStock = await StockAsync(deliverer);

        var done = await Post(deliverer, "/api/ops/deliver", new { barcode = code, deliveryPointId = DeliveryPoint });

        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        Assert.Equal("Delivered", (await Json(done)).GetProperty("status").GetString());
        Assert.Equal(inStock - 1, await StockAsync(deliverer));

        // Delivered twice: error, not a silent second row.
        var twice = await Post(deliverer, "/api/ops/deliver", new { barcode = code, deliveryPointId = DeliveryPoint });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, twice.StatusCode);
    }

    [Fact]
    public async Task Deliver_an_unknown_or_already_delivered_barcode_is_a_clear_422()
    {
        var deliverer = await SignInAsync("deliverer");

        var unknown = await Post(deliverer, "/api/ops/deliver", new { barcode = NewBarcode(), deliveryPointId = DeliveryPoint });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        Assert.Equal("unit-not-found", (await Json(unknown)).GetProperty("code").GetString());

        var delivered = await Post(deliverer, "/api/ops/deliver", new { barcode = SeededUnit, deliveryPointId = DeliveryPoint });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, delivered.StatusCode);
        Assert.Contains("already been delivered", (await Json(delivered)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Invalid_barcode_shape_is_a_422_with_a_barcode_field_error()
    {
        var receiver = await SignInAsync("receiver");

        var response = await Post(receiver, "/api/ops/receive", new { barcode = "ab", productId = Fridge, receivePointId = ReceivePoint });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("barcode", out _));
    }

    // ---- point types and archived master data ----

    [Fact]
    public async Task Service_rejects_a_point_of_the_wrong_type()
    {
        var receiver = await SignInAsync("receiver");
        var deliverer = await SignInAsync("deliverer");

        var receiveAtTrackPoint = await Post(receiver, "/api/ops/receive", new { barcode = NewBarcode(), productId = Fridge, receivePointId = TrackPoint });
        var deliverAtReceivePoint = await Post(deliverer, "/api/ops/deliver", new { barcode = SeededUnit, deliveryPointId = ReceivePoint });

        foreach (var r in new[] { receiveAtTrackPoint, deliverAtReceivePoint })
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
            Assert.Equal("wrong-point-type", (await Json(r)).GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task Archived_products_components_and_points_are_rejected()
    {
        var name = "Archived " + Guid.NewGuid().ToString("N")[..6];
        long product, component, point;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();
            var p = new Product { Name = name, IsDeleted = true, DeletedAt = DateTime.UtcNow };
            var c = new Component { Name = name, IsDeleted = true, DeletedAt = DateTime.UtcNow };
            var pt = new Point { Name = name, PointType = PointType.RECEIVE, IsDeleted = true, DeletedAt = DateTime.UtcNow };
            db.AddRange(p, c, pt);
            await db.SaveChangesAsync();
            (product, component, point) = (p.Id, c.Id, pt.Id);
        }
        var tracker = await SignInAsync("tracker");
        var receiver = await SignInAsync("receiver");

        var archivedProduct = await Post(receiver, "/api/ops/receive", new { barcode = NewBarcode(), productId = product, receivePointId = ReceivePoint });
        var archivedPoint = await Post(receiver, "/api/ops/receive", new { barcode = NewBarcode(), productId = Fridge, receivePointId = point });
        var archivedComponent = await Post(tracker, "/api/ops/track", new
        {
            barcode = NewBarcode(), productId = Fridge, trackPointId = TrackPoint,
            components = new[] { new { componentId = component, componentBarcode = NewBarcode() } },
        });

        Assert.All(new[] { archivedProduct, archivedPoint, archivedComponent },
            r => Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode));
    }

    // ---- the PostgreSQL trigger (second line of defence), bypassing the service ----

    [Fact]
    public async Task Database_trigger_rejects_wrong_point_type_taken_component_and_reversing_a_reversal()
    {
        // A scratch unit with one RECEIVED row, created through the real API.
        var receiver = await SignInAsync("receiver");
        var code = NewBarcode();
        (await Post(receiver, "/api/ops/receive", new { barcode = code, productId = Fridge, receivePointId = ReceivePoint })).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();
        var unitId = await db.Units.Where(u => u.Barcode == code).Select(u => u.Id).FirstAsync();
        var userId = await db.Users.Where(u => u.UserName == "receiver").Select(u => u.Id).FirstAsync();
        var receivedId = await db.InventoryTransactions.Where(t => t.UnitId == unitId).Select(t => t.Id).FirstAsync();
        var compressor = IdOf<Component>("Compressor");

        // (1) wrong point type: a DELIVERED row at a RECEIVE point
        var wrongPoint = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlAsync(
            $"INSERT INTO inventory_transactions (unit_id, event_type, point_id, performed_by_id, occurred_at) VALUES ({unitId}, 'DELIVERED', {ReceivePoint}, {userId}, now())"));
        Assert.Contains("needs a DELIVERY point", wrongPoint.ToString());

        // (2) a component barcode that is still attached to another unit
        var taken = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlAsync(
            $"INSERT INTO inventory_transactions (unit_id, event_type, point_id, component_id, component_barcode, performed_by_id, occurred_at) VALUES ({unitId}, 'COMPONENT_TRACKED', {TrackPoint}, {compressor}, {SeededComponentBarcode}, {userId}, now())"));
        Assert.Contains("already attached", taken.ToString());

        // (3) reversing a REVERSED row. Insert one reversal (legal) and then try to reverse that reversal.
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO inventory_transactions (unit_id, event_type, point_id, reverses_id, reason, performed_by_id, occurred_at) VALUES ({unitId}, 'REVERSED', {ReceivePoint}, {receivedId}, 'trigger test', {userId}, now())");
        var reversalId = await db.InventoryTransactions.Where(t => t.ReversesId == receivedId).Select(t => t.Id).FirstAsync();
        var reverseReversal = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlAsync(
            $"INSERT INTO inventory_transactions (unit_id, event_type, point_id, reverses_id, reason, performed_by_id, occurred_at) VALUES ({unitId}, 'REVERSED', {ReceivePoint}, {reversalId}, 'again', {userId}, now())"));
        Assert.Contains("cannot itself be reversed", reverseReversal.ToString());
    }

    // ---- authorization ----

    [Theory]
    [InlineData("/api/ops/track", "COMPONENT_TRACKER", "RECEIVE_USER,DELIVERY_USER,REPORT_VIEWER")]
    [InlineData("/api/ops/receive", "RECEIVE_USER", "COMPONENT_TRACKER,DELIVERY_USER,REPORT_VIEWER")]
    [InlineData("/api/ops/deliver", "DELIVERY_USER", "COMPONENT_TRACKER,RECEIVE_USER,REPORT_VIEWER")]
    public async Task Each_ops_endpoint_is_403_for_the_wrong_roles(string url, string allowed, string forbidden)
    {
        foreach (var role in forbidden.Split(','))
        {
            var client = await ApiTestHelpers.CreateClientAsync(factory, role);
            Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, url, new { })).StatusCode);
        }
        var ok = await ApiTestHelpers.CreateClientAsync(factory, allowed);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await Post(ok, url, new { })).StatusCode); // 400: empty body, but authorized
    }

    [Fact]
    public async Task Lookup_is_403_for_the_report_viewer()
    {
        var viewer = await ApiTestHelpers.CreateClientAsync(factory, "REPORT_VIEWER");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/units/lookup?barcode={SeededUnit}")).StatusCode);
    }

    // ---- lookup shape ----

    [Fact]
    public async Task Lookup_returns_the_documented_shape_for_a_known_and_an_unknown_barcode()
    {
        var deliverer = await SignInAsync("deliverer");

        var known = await LookupAsync(deliverer, SeededUnit.ToLowerInvariant()); // normalized on the server
        Assert.Equal(SeededUnit, known.GetProperty("barcode").GetString());
        Assert.True(known.GetProperty("exists").GetBoolean());
        Assert.Equal("Refrigerator", known.GetProperty("product").GetString());
        Assert.Equal("Delivered", known.GetProperty("status").GetString());
        Assert.Equal("Refrigerator Delivery Point 1", known.GetProperty("currentPoint").GetString());
        Assert.True(known.TryGetProperty("since", out _));
        var component = Assert.Single(known.GetProperty("components").EnumerateArray());
        Assert.Equal("Compressor", component.GetProperty("componentName").GetString());
        Assert.Equal(SeededComponentBarcode, component.GetProperty("componentBarcode").GetString());
        Assert.Equal(3, known.GetProperty("recentEvents").GetArrayLength());
        Assert.Equal("DELIVERED", known.GetProperty("recentEvents")[0].GetProperty("eventType").GetString()); // newest first

        var unknown = await LookupAsync(deliverer, NewBarcode());
        Assert.False(unknown.GetProperty("exists").GetBoolean());
        Assert.Equal("NotFound", unknown.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, unknown.GetProperty("product").ValueKind);

        Assert.Equal(HttpStatusCode.BadRequest, (await deliverer.GetAsync("/api/units/lookup?barcode=x")).StatusCode);
    }

    // ---- history filters ----

    [Fact]
    public async Task History_filters_by_event_search_user_and_local_date_range_and_pages()
    {
        var viewer = await SignInAsync("viewer");
        async Task<JsonElement> Get(string query) => await Json(await viewer.GetAsync("/api/history?" + query));

        var delivered = await Get($"eventType=DELIVERED&q={SeededUnit}");
        Assert.Equal(1, delivered.GetProperty("totalCount").GetInt32());
        Assert.Equal("DELIVERED", delivered.GetProperty("items")[0].GetProperty("eventType").GetString());

        // Search by component barcode (case-insensitive, part of the text).
        Assert.Equal(1, (await Get("q=94a6003")).GetProperty("totalCount").GetInt32());

        // Sample record: tracked 2026-03-27 10:00 Dhaka. Day boundaries are local days.
        Assert.Equal(1, (await Get($"from=2026-03-27&to=2026-03-27&q={SeededUnit}")).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await Get($"to=2026-03-26&q={SeededUnit}")).GetProperty("totalCount").GetInt32());
        Assert.Equal(3, (await Get($"from=2026-03-27&to=2026-03-30&q={SeededUnit}")).GetProperty("totalCount").GetInt32());

        // User + point filter use ids from the filter-options endpoint (the viewer may call it).
        var options = await Json(await viewer.GetAsync("/api/history/filter-options"));
        long Find(string list, string contains) => options.GetProperty(list).EnumerateArray()
            .First(o => o.GetProperty("name").GetString()!.Contains(contains)).GetProperty("id").GetInt64();
        var byTracker = await Get($"userId={Find("users", "66798")}&q={SeededUnit}");
        Assert.Equal(1, byTracker.GetProperty("totalCount").GetInt32());
        var atPoint = await Get($"pointId={Find("points", "Receive Point 1")}&q={SeededUnit}");
        Assert.Equal(1, atPoint.GetProperty("totalCount").GetInt32());

        var paged = await Get($"pageSize=2&page=2&q={SeededUnit}&sort=time");
        Assert.Equal(1, paged.GetProperty("items").GetArrayLength()); // 3 rows, 2 per page -> page 2 has 1
        Assert.Equal(3, paged.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task History_is_403_for_operational_roles()
    {
        var tracker = await ApiTestHelpers.CreateClientAsync(factory, "COMPONENT_TRACKER");
        Assert.Equal(HttpStatusCode.Forbidden, (await tracker.GetAsync("/api/history")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tracker.GetAsync("/api/history/filter-options")).StatusCode);
    }

    // ---- service-level: derived status agrees with the lookup ----

    [Fact]
    public async Task Service_status_matches_the_operations()
    {
        using var scope = factory.Services.CreateScope();
        var inventory = scope.ServiceProvider.GetRequiredService<InventoryService>();
        Assert.Equal(UnitState.Delivered, (await inventory.GetUnitStatusAsync(SeededUnit)).State);
        Assert.Equal(UnitState.NotFound, (await inventory.GetUnitStatusAsync(NewBarcode())).State);
    }
}
