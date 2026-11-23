using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ims.Api.Data;
using Ims.Api.Domain;
using Ims.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ims.Tests;

/// <summary>Day 5: audit log (R13), the dashboard metrics and the end-to-end sample record.</summary>
[Collection(ApiCollection.Name)]
public class AuditAndDashboardTests(ImsApiFactory factory)
{
    private async Task<HttpClient> SignInAsync(string user)
    {
        var client = await ApiTestHelpers.CreateClientAsync(factory);
        (await ApiTestHelpers.LoginAsync(client, user, user == "admin" ? "Admin@12345" : "Demo@12345")).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];

    /// <summary>Audit rows whose entity name or action matches the search text, newest first.</summary>
    private static async Task<List<JsonElement>> AuditAsync(HttpClient admin, string q, string extra = "")
    {
        var page = await Json(await admin.GetAsync($"/api/audit?q={Uri.EscapeDataString(q)}&pageSize=100{extra}"));
        return page.GetProperty("items").EnumerateArray().ToList();
    }

    private static async Task<JsonElement> DetailAsync(HttpClient admin, JsonElement row) =>
        await Json(await admin.GetAsync($"/api/audit/{row.GetProperty("id").GetInt64()}"));

    private long IdOf<T>(string name) where T : MasterDataEntity
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ImsDbContext>().Set<T>().Where(x => x.Name == name).Select(x => x.Id).First();
    }

    // ---- master data ----

    [Fact]
    public async Task Creating_and_renaming_a_product_writes_audit_rows_with_old_and_new_name_and_the_actor()
    {
        var admin = await SignInAsync("admin");
        var name = Unique("AuditProd");
        var created = await Json(await admin.PostAsJsonAsync("/api/products", new { name }));
        var id = created.GetProperty("id").GetInt64();

        (await admin.PutAsJsonAsync($"/api/products/{id}", new { name = name + "X" })).EnsureSuccessStatusCode();

        var rows = await AuditAsync(admin, name);
        var update = rows.Single(r => r.GetProperty("action").GetString() == "UPDATE");
        var detail = await DetailAsync(admin, update);
        Assert.Equal(name, detail.GetProperty("oldValues").GetProperty("name").GetString());
        Assert.Equal(name + "X", detail.GetProperty("newValues").GetProperty("name").GetString());
        Assert.Single(detail.GetProperty("newValues").EnumerateObject()); // ONLY the changed property
        Assert.Contains("10001", detail.GetProperty("actor").GetString()); // the admin
        Assert.Equal("Product", detail.GetProperty("entityType").GetString());
        Assert.Equal(id.ToString(), detail.GetProperty("entityId").GetString());

        var create = rows.Single(r => r.GetProperty("action").GetString() == "CREATE");
        Assert.Equal(id.ToString(), create.GetProperty("entityId").GetString()); // the generated key was captured
        Assert.Equal(name, (await DetailAsync(admin, create)).GetProperty("newValues").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Soft_delete_restore_and_hard_delete_get_their_own_action_names()
    {
        var admin = await SignInAsync("admin");
        var receiver = await SignInAsync("receiver");
        var name = Unique("AuditUsed");
        var id = (await Json(await admin.PostAsJsonAsync("/api/products", new { name }))).GetProperty("id").GetInt64();
        var barcode = "AU" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        (await receiver.PostAsJsonAsync("/api/ops/receive", new { barcode, productId = id, receivePointId = IdOf<Point>("Refrigerator Receive Point 1") })).EnsureSuccessStatusCode();

        await admin.DeleteAsync($"/api/products/{id}");   // in use -> archived
        await admin.PostAsync($"/api/products/{id}/restore", null);
        var loose = Unique("AuditLoose");
        var looseId = (await Json(await admin.PostAsJsonAsync("/api/products", new { name = loose }))).GetProperty("id").GetInt64();
        await admin.DeleteAsync($"/api/products/{looseId}"); // not in use -> really deleted

        var actions = (await AuditAsync(admin, name)).Select(r => r.GetProperty("action").GetString()).ToList();
        Assert.Contains("SOFT_DELETE", actions);
        Assert.Contains("RESTORE", actions);
        var soft = await DetailAsync(admin, (await AuditAsync(admin, name)).First(r => r.GetProperty("action").GetString() == "SOFT_DELETE"));
        Assert.False(soft.GetProperty("oldValues").GetProperty("isDeleted").GetBoolean());
        Assert.True(soft.GetProperty("newValues").GetProperty("isDeleted").GetBoolean());

        var deleteRow = (await AuditAsync(admin, loose)).Single(r => r.GetProperty("action").GetString() == "DELETE");
        Assert.Equal(loose, (await DetailAsync(admin, deleteRow)).GetProperty("oldValues").GetProperty("name").GetString());
    }

    // ---- users ----

    [Fact]
    public async Task Role_change_deactivate_and_activate_are_logged_with_old_and_new_values()
    {
        var admin = await SignInAsync("admin");
        var userName = Unique("audu");
        var employeeNo = Random.Shared.Next(100000, 999999).ToString();
        var user = await Json(await admin.PostAsJsonAsync("/api/users", new
        {
            userName, firstName = "Audit", lastName = "Subject", employeeNo, role = "RECEIVE_USER", password = "Demo@12345",
        }));
        var id = user.GetProperty("id").GetInt64();

        (await admin.PutAsJsonAsync($"/api/users/{id}", new { firstName = "Audit", lastName = "Subject", employeeNo, role = "DELIVERY_USER" })).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/users/{id}/toggle-active", null)).EnsureSuccessStatusCode(); // deactivate
        (await admin.PostAsync($"/api/users/{id}/toggle-active", null)).EnsureSuccessStatusCode(); // activate

        var rows = await AuditAsync(admin, userName);
        var role = await DetailAsync(admin, rows.Single(r => r.GetProperty("action").GetString() == "ROLE_CHANGE"));
        Assert.Equal("RECEIVE_USER", role.GetProperty("oldValues").GetProperty("role").GetString());
        Assert.Equal("DELIVERY_USER", role.GetProperty("newValues").GetProperty("role").GetString());

        var deactivate = await DetailAsync(admin, rows.Single(r => r.GetProperty("action").GetString() == "DEACTIVATE"));
        Assert.True(deactivate.GetProperty("oldValues").GetProperty("isActive").GetBoolean());
        Assert.False(deactivate.GetProperty("newValues").GetProperty("isActive").GetBoolean());
        Assert.Contains(rows, r => r.GetProperty("action").GetString() == "ACTIVATE");
        Assert.Equal("User", rows[0].GetProperty("entityType").GetString());
    }

    [Fact]
    public async Task Password_change_and_reset_are_logged_without_any_values()
    {
        var admin = await SignInAsync("admin");
        var userName = Unique("audp");
        var employeeNo = Random.Shared.Next(100000, 999999).ToString();
        var id = (await Json(await admin.PostAsJsonAsync("/api/users", new
        {
            userName, firstName = "Pw", lastName = "Subject", employeeNo, role = "REPORT_VIEWER", password = "Demo@12345",
        }))).GetProperty("id").GetInt64();

        (await admin.PostAsJsonAsync($"/api/users/{id}/reset-password", new { newPassword = "Other@12345" })).EnsureSuccessStatusCode();

        var own = await ApiTestHelpers.CreateClientAsync(factory);
        (await ApiTestHelpers.LoginAsync(own, userName, "Other@12345")).EnsureSuccessStatusCode();
        (await own.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "Other@12345", newPassword = "Third@12345", confirmPassword = "Third@12345" })).EnsureSuccessStatusCode();

        var rows = await AuditAsync(admin, userName);
        var reset = rows.Single(r => r.GetProperty("action").GetString() == "PASSWORD_RESET");
        var change = rows.Single(r => r.GetProperty("action").GetString() == "PASSWORD_CHANGE");
        foreach (var row in new[] { reset, change })
        {
            var detail = await DetailAsync(admin, row);
            Assert.Equal(JsonValueKind.Null, detail.GetProperty("oldValues").ValueKind);
            Assert.Equal(JsonValueKind.Null, detail.GetProperty("newValues").ValueKind);
        }
        Assert.Contains("10001", (await DetailAsync(admin, reset)).GetProperty("actor").GetString());   // the admin reset it
        Assert.Contains(userName, (await DetailAsync(admin, change)).GetProperty("entityRepr").GetString());
    }

    // ---- logins ----

    [Fact]
    public async Task Login_logout_and_failed_login_are_logged_and_no_password_appears_anywhere()
    {
        var admin = await SignInAsync("admin");
        var client = await ApiTestHelpers.CreateClientAsync(factory);
        (await ApiTestHelpers.LoginAsync(client, "tracker", "Wrong-Password-123!")).StatusCode.ToString();
        (await ApiTestHelpers.LoginAsync(client, "tracker", "Demo@12345")).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();
        var ghost = Unique("ghost");
        await ApiTestHelpers.LoginAsync(await ApiTestHelpers.CreateClientAsync(factory), ghost, "Secret-Ghost-1!");

        var tracker = await AuditAsync(admin, "tracker");
        Assert.Contains(tracker, r => r.GetProperty("action").GetString() == "LOGIN");
        Assert.Contains(tracker, r => r.GetProperty("action").GetString() == "LOGOUT");
        Assert.Contains(tracker, r => r.GetProperty("action").GetString() == "LOGIN_FAILED");
        var failedGhost = (await AuditAsync(admin, ghost)).Single();
        Assert.Equal("LOGIN_FAILED", failedGhost.GetProperty("action").GetString());
        Assert.Equal(JsonValueKind.Null, failedGhost.GetProperty("actorId").ValueKind); // unknown user -> no actor

        // Nothing that looks like a password or hash is stored in ANY column of ANY audit row.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();
        var everything = string.Join('\n', (await db.AuditLogs.AsNoTracking().ToListAsync())
            .Select(a => a.Action + a.EntityType + a.EntityId + a.EntityRepr + a.OldValues + a.NewValues));
        foreach (var secret in new[] { "Demo@12345", "Admin@12345", "Other@12345", "Third@12345", "Wrong-Password-123!", "Secret-Ghost-1!" })
            Assert.DoesNotContain(secret, everything);
        foreach (var field in new[] { "passwordHash", "PasswordHash", "securityStamp", "SecurityStamp", "concurrencyStamp", "AQAAAA" })
            Assert.DoesNotContain(field, everything);
    }

    // ---- append-only ----

    [Fact]
    public async Task Audit_rows_cannot_be_updated_or_deleted_not_by_EF_and_not_by_raw_SQL()
    {
        var admin = await SignInAsync("admin");
        await AuditAsync(admin, "x"); // make sure at least one row exists
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();

        var row = await db.AuditLogs.OrderBy(a => a.Id).FirstAsync();
        row.Action = "TAMPERED";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); // AppendOnlyInterceptor
        db.ChangeTracker.Clear();

        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE audit_log SET action = 'TAMPERED'")); // DB trigger
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM audit_log"));
        Assert.Equal(0, await db.AuditLogs.CountAsync(a => a.Action == "TAMPERED"));
    }

    // ---- who may see it, paging, filters, export ----

    [Theory]
    [InlineData("viewer")] [InlineData("tracker")] [InlineData("receiver")] [InlineData("deliverer")]
    public async Task Only_admins_may_read_or_export_the_audit_log(string user)
    {
        var client = await SignInAsync(user);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit?format=csv")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit/filter-options")).StatusCode);
    }

    [Fact]
    public async Task Audit_list_filters_by_action_entity_type_actor_and_date_and_pages()
    {
        var admin = await SignInAsync("admin");
        var name = Unique("AuditFilt");
        var id = (await Json(await admin.PostAsJsonAsync("/api/products", new { name }))).GetProperty("id").GetInt64();
        await admin.PutAsJsonAsync($"/api/products/{id}", new { name = name + "2" });
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        Assert.Single(await AuditAsync(admin, name, "&action=UPDATE"));
        Assert.Equal(2, (await AuditAsync(admin, name, "&entityType=Product")).Count);
        Assert.Empty(await AuditAsync(admin, name, "&entityType=Component"));
        Assert.Equal(2, (await AuditAsync(admin, name, "&actorId=1")).Count);
        Assert.Empty(await AuditAsync(admin, name, "&to=2026-01-01"));
        Assert.Equal(2, (await AuditAsync(admin, name, $"&from={today}")).Count);

        var paged = await Json(await admin.GetAsync($"/api/audit?q={name}&pageSize=1"));
        Assert.Equal(1, paged.GetProperty("items").GetArrayLength());
        Assert.Equal(2, paged.GetProperty("totalCount").GetInt32());

        var options = await Json(await admin.GetAsync("/api/audit/filter-options"));
        Assert.Contains("UPDATE", options.GetProperty("actions").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/audit/999999999")).StatusCode);
    }

    [Fact]
    public async Task Audit_export_follows_the_filters()
    {
        var admin = await SignInAsync("admin");
        var name = Unique("AuditExp");
        await admin.PostAsJsonAsync("/api/products", new { name });

        var response = await admin.GetAsync($"/api/audit?format=csv&q={name}");

        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        var lines = new UTF8Encoding(false).GetString(await response.Content.ReadAsByteArrayAsync()).TrimStart('﻿')
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Time,Actor,Action,Entity type", lines[0]);
        Assert.Equal(2, lines.Length);
        Assert.Contains("CREATE", lines[1]);
    }

    // ---- dashboard ----

    [Fact]
    public async Task Dashboard_metrics_move_with_a_receive_and_match_the_stock_report()
    {
        var admin = await SignInAsync("admin");
        var receiver = await SignInAsync("receiver");
        var before = await Json(await admin.GetAsync("/api/dashboard"));

        var barcode = "DB" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        (await receiver.PostAsJsonAsync("/api/ops/receive", new { barcode, productId = IdOf<Product>("Freezer"), receivePointId = IdOf<Point>("Refrigerator Receive Point 2") })).EnsureSuccessStatusCode();
        var after = await Json(await admin.GetAsync("/api/dashboard"));

        int N(JsonElement e, string p) => e.GetProperty(p).GetInt32();
        Assert.Equal(N(before, "stockCount") + 1, N(after, "stockCount"));
        Assert.Equal(N(before, "todayReceived") + 1, N(after, "todayReceived"));
        Assert.Equal(N(before, "unitsRegisteredToday") + 1, N(after, "unitsRegisteredToday"));
        Assert.Equal(N(before, "totalUnits") + 1, N(after, "totalUnits"));
        Assert.Equal(N(after, "stockCount"), after.GetProperty("stockByPoint").EnumerateArray().Sum(p => p.GetProperty("count").GetInt32()));
        var stock = await Json(await admin.GetAsync("/api/reports/stock?pageSize=1"));
        Assert.Equal(N(after, "stockCount"), N(stock, "totalCount")); // same numbers as the report

        var stuck = after.GetProperty("stuckLongest").EnumerateArray().Select(s => s.GetProperty("daysInStock").GetInt32()).ToList();
        Assert.True(stuck.Count <= 10);
        Assert.Equal(stuck.OrderByDescending(d => d), stuck); // longest first
    }

    [Fact]
    public async Task Dashboard_counts_a_delivery_and_a_reversal_today_and_stops_counting_a_reversed_receive()
    {
        var admin = await SignInAsync("admin");
        var receiver = await SignInAsync("receiver");
        var deliverer = await SignInAsync("deliverer");
        var before = await Json(await admin.GetAsync("/api/dashboard"));
        var barcode = "DC" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        (await receiver.PostAsJsonAsync("/api/ops/receive", new { barcode, productId = IdOf<Product>("Freezer"), receivePointId = IdOf<Point>("Refrigerator Receive Point 1") })).EnsureSuccessStatusCode();
        (await deliverer.PostAsJsonAsync("/api/ops/deliver", new { barcode, deliveryPointId = IdOf<Point>("Refrigerator Delivery Point 1") })).EnsureSuccessStatusCode();

        var delivered = await Json(await admin.GetAsync("/api/dashboard"));
        Assert.Equal(before.GetProperty("todayDelivered").GetInt32() + 1, delivered.GetProperty("todayDelivered").GetInt32());

        var deliveredRow = (await Json(await admin.GetAsync($"/api/history?q={barcode}"))).GetProperty("items").EnumerateArray()
            .First(r => r.GetProperty("eventType").GetString() == "DELIVERED").GetProperty("id").GetInt64();
        (await admin.PostAsJsonAsync($"/api/history/{deliveredRow}/reverse", new { reason = "Customer cancelled" })).EnsureSuccessStatusCode();

        var reversed = await Json(await admin.GetAsync("/api/dashboard"));
        Assert.Equal(before.GetProperty("todayDelivered").GetInt32(), reversed.GetProperty("todayDelivered").GetInt32()); // the delivery no longer counts
        Assert.Equal(before.GetProperty("todayReversed").GetInt32() + 1, reversed.GetProperty("todayReversed").GetInt32());
        Assert.Equal(before.GetProperty("stockCount").GetInt32() + 1, reversed.GetProperty("stockCount").GetInt32()); // back in stock
    }

    [Fact]
    public async Task Dashboard_recent_activity_is_only_returned_to_roles_with_history_access()
    {
        foreach (var (user, expectRecent) in new[] { ("admin", true), ("viewer", true), ("tracker", false), ("receiver", false), ("deliverer", false) })
        {
            var dashboard = await Json(await (await SignInAsync(user)).GetAsync("/api/dashboard"));
            var recent = dashboard.GetProperty("recent").GetArrayLength();
            Assert.Equal(expectRecent, recent > 0);
            Assert.True(recent <= 10);
        }
    }

    // ---- the brief's sample record, end to end ----

    [Fact]
    public async Task Sample_record_end_to_end_matches_the_brief()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();
            var rows = await db.InventoryTransactions.AsNoTracking()
                .Where(t => t.Unit!.Barcode == "2440701AK2057").OrderBy(t => t.OccurredAt).ThenBy(t => t.Id)
                .Select(t => new
                {
                    t.EventType, Point = t.Point!.Name, Employee = t.PerformedBy!.EmployeeNo, t.ComponentBarcode,
                    Component = t.Component != null ? t.Component.Name : null, Product = t.Unit!.Product!.Name, t.OccurredAt,
                }).ToListAsync();

            Assert.Equal([EventType.COMPONENT_TRACKED, EventType.RECEIVED, EventType.DELIVERED], rows.Select(r => r.EventType).ToArray());
            Assert.Equal(["Refrigerator Component Track Point 1", "Refrigerator Receive Point 1", "Refrigerator Delivery Point 1"], rows.Select(r => r.Point).ToArray());
            Assert.Equal(["66798", "58877", "54671"], rows.Select(r => r.Employee).ToArray());
            Assert.All(rows, r => Assert.Equal("Refrigerator", r.Product));
            Assert.Equal("Compressor", rows[0].Component);
            Assert.Equal("94A600321852", rows[0].ComponentBarcode);

            // The brief's times are LOCAL times in the configured zone.
            var time = scope.ServiceProvider.GetRequiredService<AppTime>();
            Assert.Equal(["2026-03-27 10:00:00", "2026-03-28 10:15:00", "2026-03-30 11:30:00"], rows.Select(r => time.Format(r.OccurredAt)).ToArray());

            var inventory = scope.ServiceProvider.GetRequiredService<InventoryService>();
            Assert.Equal(UnitState.Delivered, (await inventory.GetUnitStatusAsync("2440701AK2057")).State);
        }

        var viewer = await SignInAsync("viewer");
        var report = await Json(await viewer.GetAsync("/api/reports/barcode?barcode=2440701AK2057"));
        Assert.Equal(3, report.GetProperty("timeline").GetArrayLength());
        var summary = await Json(await viewer.GetAsync("/api/reports/summary?from=2026-03-27&to=2026-03-30"));
        var totals = summary.GetProperty("totals");
        Assert.Equal([1, 1, 1], new[] { totals.GetProperty("tracked").GetInt32(), totals.GetProperty("received").GetInt32(), totals.GetProperty("delivered").GetInt32() });
    }
}
