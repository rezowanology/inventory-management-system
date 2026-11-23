using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ims.Api.Data;
using Ims.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ims.Tests;

/// <summary>Day 2: master data CRUD, soft delete, users, forced logout, password change.</summary>
[Collection(ApiCollection.Name)]
public class AdminTests(ImsApiFactory factory)
{
    private static string Unique() => Guid.NewGuid().ToString("N")[..8];
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();
    private Task<HttpClient> AdminAsync() => ApiTestHelpers.CreateClientAsync(factory, "ADMIN");

    private static async Task<long> CreateAsync(HttpClient admin, string url, string name, string? type = null)
    {
        var response = await admin.PostAsJsonAsync(url, new { name, type });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetInt64();
    }

    private static async Task<List<string>> OptionNames(HttpClient client, string url) =>
        (await Json(await client.GetAsync(url))).EnumerateArray().Select(o => o.GetProperty("name").GetString()!).ToList();

    // ---------------- master data ----------------

    [Fact]
    public async Task Product_create_update_search_and_paging()
    {
        var admin = await AdminAsync();
        var tag = "Pg" + Unique();
        foreach (var n in new[] { "A", "B", "C" }) await CreateAsync(admin, "/api/products", $"{tag} {n}");

        var page1 = await Json(await admin.GetAsync($"/api/products?q={tag.ToLower()}&pageSize=2&page=1"));
        Assert.Equal(3, page1.GetProperty("totalCount").GetInt32()); // search is case-insensitive
        Assert.Equal(2, page1.GetProperty("items").GetArrayLength());
        var page2 = await Json(await admin.GetAsync($"/api/products?q={tag}&pageSize=2&page=2"));
        Assert.Equal(1, page2.GetProperty("items").GetArrayLength());

        var id = page1.GetProperty("items")[0].GetProperty("id").GetInt64();
        var update = await admin.PutAsJsonAsync($"/api/products/{id}", new { name = $"{tag} Renamed" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal($"{tag} Renamed", (await Json(update)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Duplicate_names_are_rejected_case_insensitively_with_a_field_error()
    {
        var admin = await AdminAsync();
        var response = await admin.PostAsJsonAsync("/api/components", new { name = " compressor " }); // seeded "Compressor"
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("name", out _));

        // updating a row to a name that another row has is rejected too
        var id = await CreateAsync(admin, "/api/components", "Dup" + Unique());
        var update = await admin.PutAsJsonAsync($"/api/components/{id}", new { name = "CONDENSER" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, update.StatusCode);
    }

    [Fact]
    public async Task Name_validation_rules_give_400()
    {
        var admin = await AdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/products", new { name = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/products", new { name = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/products", new { name = new string('a', 101) })).StatusCode);
        // "  a " is long enough before trimming but only 1 character after it -> business-rule 422
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync("/api/products", new { name = "  a " })).StatusCode);
    }

    [Fact]
    public async Task Unused_product_is_hard_deleted()
    {
        var admin = await AdminAsync();
        var id = await CreateAsync(admin, "/api/products", "Temp" + Unique());

        var delete = await admin.DeleteAsync($"/api/products/{id}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.False((await Json(delete)).GetProperty("archived").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/products/{id}")).StatusCode);
    }

    [Fact]
    public async Task Product_used_by_a_unit_is_archived_hidden_from_options_and_restorable()
    {
        var admin = await AdminAsync();
        var name = "InUse" + Unique();
        var id = await CreateAsync(admin, "/api/products", name);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();
            var someUser = await db.Users.FirstAsync();
            db.Units.Add(new Unit { Barcode = "UT" + Unique().ToUpper(), ProductId = id, RegisteredAt = DateTime.UtcNow, RegisteredById = someUser.Id });
            await db.SaveChangesAsync();
        }
        Assert.Contains(name, await OptionNames(admin, "/api/products/options"));

        var delete = await admin.DeleteAsync($"/api/products/{id}");
        Assert.True((await Json(delete)).GetProperty("archived").GetBoolean());
        Assert.DoesNotContain(name, await OptionNames(admin, "/api/products/options")); // gone from dropdowns
        Assert.True((await Json(await admin.GetAsync($"/api/products/{id}"))).GetProperty("isDeleted").GetBoolean());

        var archived = await Json(await admin.GetAsync($"/api/products?status=deleted&q={name}"));
        Assert.Equal(1, archived.GetProperty("totalCount").GetInt32());
        var activeOnly = await Json(await admin.GetAsync($"/api/products?q={name}"));
        Assert.Equal(0, activeOnly.GetProperty("totalCount").GetInt32());

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/products/{id}/restore", null)).StatusCode);
        Assert.Contains(name, await OptionNames(admin, "/api/products/options"));
    }

    [Fact]
    public async Task Archiving_the_sample_refrigerator_keeps_it_in_history_and_restore_brings_it_back()
    {
        var admin = await AdminAsync();
        var list = await Json(await admin.GetAsync("/api/products?q=Refrigerator"));
        var id = list.GetProperty("items")[0].GetProperty("id").GetInt64();
        try
        {
            Assert.True((await Json(await admin.DeleteAsync($"/api/products/{id}"))).GetProperty("archived").GetBoolean());
            Assert.DoesNotContain("Refrigerator", await OptionNames(admin, "/api/products/options"));

            var history = await Json(await admin.GetAsync("/api/history?q=2440701AK2057")); // the sample unit; other tests add Freezer / A/C units
            Assert.All(history.GetProperty("items").EnumerateArray(),
                row => Assert.Equal("Refrigerator", row.GetProperty("productName").GetString()));
        }
        finally { await admin.PostAsync($"/api/products/{id}/restore", null); }
        Assert.Contains("Refrigerator", await OptionNames(admin, "/api/products/options"));
    }

    [Fact]
    public async Task Restore_is_refused_when_an_active_row_took_the_name()
    {
        var admin = await AdminAsync();
        var name = "Twin" + Unique();
        var id = await CreateAsync(admin, "/api/components", name);
        await using (var scope = factory.Services.CreateAsyncScope()) // archive it directly (it has no history to be "in use")
        {
            var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();
            var row = await db.Components.SingleAsync(c => c.Id == id);
            row.IsDeleted = true;
            await db.SaveChangesAsync();
        }
        await CreateAsync(admin, "/api/components", name); // allowed: the archived row does not count
        var restore = await admin.PostAsync($"/api/components/{id}/restore", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, restore.StatusCode);
    }

    [Fact]
    public async Task Points_have_types_filter_by_type_and_the_type_is_required()
    {
        var admin = await AdminAsync();
        var name = "Pt" + Unique();
        var trackId = await CreateAsync(admin, "/api/points", name, "TRACK");
        await CreateAsync(admin, "/api/points", name, "RECEIVE"); // same name, other type: allowed

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await admin.PostAsJsonAsync("/api/points", new { name, type = "TRACK" })).StatusCode); // same name AND type
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await admin.PostAsJsonAsync("/api/points", new { name = "NoType" + Unique() })).StatusCode);

        var tracks = await Json(await admin.GetAsync($"/api/points?q={name}&type=TRACK"));
        Assert.Equal(1, tracks.GetProperty("totalCount").GetInt32());
        Assert.Equal(trackId, tracks.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Contains(name, await OptionNames(admin, "/api/points/options?type=RECEIVE"));
        Assert.DoesNotContain("Refrigerator Delivery Point 1", await OptionNames(admin, "/api/points/options?type=RECEIVE"));
    }

    [Fact]
    public async Task Point_used_in_history_is_archived_and_its_type_is_locked()
    {
        var admin = await AdminAsync();
        var list = await Json(await admin.GetAsync("/api/points?q=Refrigerator Receive Point 1"));
        var id = list.GetProperty("items")[0].GetProperty("id").GetInt64();
        try
        {
            var change = await admin.PutAsJsonAsync($"/api/points/{id}", new { name = "Refrigerator Receive Point 1", type = "DELIVERY" });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, change.StatusCode);
            Assert.True((await Json(await admin.DeleteAsync($"/api/points/{id}"))).GetProperty("archived").GetBoolean());
        }
        finally { await admin.PostAsync($"/api/points/{id}/restore", null); }
    }

    [Fact]
    public async Task Options_are_open_to_operational_roles_but_not_to_the_viewer()
    {
        var tracker = await ApiTestHelpers.CreateClientAsync(factory, "COMPONENT_TRACKER");
        Assert.Equal(HttpStatusCode.OK, (await tracker.GetAsync("/api/components/options")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tracker.GetAsync("/api/components")).StatusCode);

        var viewer = await ApiTestHelpers.CreateClientAsync(factory, "REPORT_VIEWER");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/components/options")).StatusCode);
    }

    [Theory]
    [InlineData("COMPONENT_TRACKER")]
    [InlineData("RECEIVE_USER")]
    [InlineData("DELIVERY_USER")]
    [InlineData("REPORT_VIEWER")]
    public async Task Non_admin_roles_cannot_use_admin_endpoints(string role)
    {
        var client = await ApiTestHelpers.CreateClientAsync(factory, role);
        foreach (var url in new[] { "/api/products", "/api/components", "/api/points", "/api/users" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/products", new { name = "Hack" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/users/1/toggle-active", null)).StatusCode);
    }

    // ---------------- users ----------------

    private record NewUser(long Id, string UserName, string Password);

    private async Task<NewUser> CreateUserAsync(HttpClient admin, string role, string password = "Start@12345")
    {
        var u = "u" + Unique();
        var response = await admin.PostAsJsonAsync("/api/users", new
        {
            userName = u, firstName = "Test", lastName = "User", employeeNo = "T" + Unique(), role, password
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return new NewUser((await Json(response)).GetProperty("id").GetInt64(), u, password);
    }

    private async Task<HttpClient> LoginClientAsync(string userName, string password)
    {
        var client = await ApiTestHelpers.CreateClientAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await ApiTestHelpers.LoginAsync(client, userName, password)).StatusCode);
        return client;
    }

    [Fact]
    public async Task Admin_creates_a_user_with_one_role_and_can_change_the_role_later()
    {
        var admin = await AdminAsync();
        var user = await CreateUserAsync(admin, "RECEIVE_USER");

        var me = await Json(await (await LoginClientAsync(user.UserName, user.Password)).GetAsync("/api/auth/me"));
        Assert.Equal("RECEIVE_USER", me.GetProperty("role").GetString());

        var update = await admin.PutAsJsonAsync($"/api/users/{user.Id}",
            new { firstName = "New", lastName = "Name", employeeNo = "N" + Unique(), role = "DELIVERY_USER" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("DELIVERY_USER", (await Json(update)).GetProperty("role").GetString());

        // the user's NEXT request already carries the new role
        var client = await LoginClientAsync(user.UserName, user.Password);
        Assert.Equal("DELIVERY_USER", (await Json(await client.GetAsync("/api/auth/me"))).GetProperty("role").GetString());
    }

    [Fact]
    public async Task User_create_rules_duplicate_employee_no_weak_password_and_missing_role()
    {
        var admin = await AdminAsync();
        var duplicate = await admin.PostAsJsonAsync("/api/users", new
        { userName = "d" + Unique(), firstName = "A", lastName = "B", employeeNo = "10001", role = "REPORT_VIEWER", password = "Start@12345" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.StatusCode);
        Assert.True((await Json(duplicate)).GetProperty("errors").TryGetProperty("employeeNo", out _));

        var weak = await admin.PostAsJsonAsync("/api/users", new
        { userName = "w" + Unique(), firstName = "A", lastName = "B", employeeNo = "W" + Unique(), role = "REPORT_VIEWER", password = "weak" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, weak.StatusCode);
        Assert.True((await Json(weak)).GetProperty("errors").TryGetProperty("password", out _));

        var noRole = await admin.PostAsJsonAsync("/api/users", new
        { userName = "n" + Unique(), firstName = "A", lastName = "B", employeeNo = "R" + Unique(), password = "Start@12345" });
        Assert.Equal(HttpStatusCode.BadRequest, noRole.StatusCode); // must not silently become ADMIN
    }

    [Fact]
    public async Task User_list_search_and_filters()
    {
        var admin = await AdminAsync();
        var user = await CreateUserAsync(admin, "DELIVERY_USER");

        var byName = await Json(await admin.GetAsync($"/api/users?q={user.UserName.ToUpper()}"));
        Assert.Equal(1, byName.GetProperty("totalCount").GetInt32());

        var delivery = await Json(await admin.GetAsync("/api/users?role=DELIVERY_USER&active=true&pageSize=200"));
        Assert.All(delivery.GetProperty("items").EnumerateArray(), u => Assert.Equal("DELIVERY_USER", u.GetProperty("role").GetString()));
    }

    [Fact]
    public async Task Self_registered_user_is_pending_until_activated_then_can_log_in()
    {
        var admin = await AdminAsync();
        var anon = await ApiTestHelpers.CreateClientAsync(factory);
        var name = "reg" + Unique();
        var register = await anon.PostAsJsonAsync("/api/auth/register", new
        { userName = name, password = "Newbie@12345", firstName = "Re", lastName = "Gistered", employeeNo = "G" + Unique() });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var pending = await Json(await admin.GetAsync($"/api/users?pending=true&q={name}"));
        Assert.Equal(1, pending.GetProperty("totalCount").GetInt32());
        var id = pending.GetProperty("items")[0].GetProperty("id").GetInt64();
        Assert.Equal("REPORT_VIEWER", pending.GetProperty("items")[0].GetProperty("role").GetString());

        Assert.Equal(HttpStatusCode.Forbidden, (await ApiTestHelpers.LoginAsync(anon, name, "Newbie@12345")).StatusCode);

        var toggle = await admin.PostAsync($"/api/users/{id}/toggle-active", null);
        Assert.True((await Json(toggle)).GetProperty("isActive").GetBoolean());
        await LoginClientAsync(name, "Newbie@12345");

        var after = await Json(await admin.GetAsync($"/api/users?pending=true&q={name}"));
        Assert.Equal(0, after.GetProperty("totalCount").GetInt32()); // has logged in now
    }

    [Fact]
    public async Task Deactivating_a_user_ends_their_existing_session_and_blocks_login_until_reactivated()
    {
        var admin = await AdminAsync();
        var victim = await CreateUserAsync(admin, "RECEIVE_USER");
        var victimClient = await LoginClientAsync(victim.UserName, victim.Password); // a real cookie session
        Assert.Equal(HttpStatusCode.OK, (await victimClient.GetAsync("/api/auth/me")).StatusCode);

        var off = await admin.PostAsync($"/api/users/{victim.Id}/toggle-active", null);
        Assert.False((await Json(off)).GetProperty("isActive").GetBoolean());

        // The cookie the victim already holds is rejected on the very next call (security stamp changed).
        Assert.Equal(HttpStatusCode.Unauthorized, (await victimClient.GetAsync("/api/auth/me")).StatusCode);

        var retry = await ApiTestHelpers.LoginAsync(await ApiTestHelpers.CreateClientAsync(factory), victim.UserName, victim.Password);
        Assert.Equal(HttpStatusCode.Forbidden, retry.StatusCode);
        Assert.Equal("This account is not active", (await Json(retry)).GetProperty("title").GetString());

        await admin.PostAsync($"/api/users/{victim.Id}/toggle-active", null);
        await LoginClientAsync(victim.UserName, victim.Password);
    }

    [Fact]
    public async Task An_admin_cannot_deactivate_themselves()
    {
        var admin = await AdminAsync();
        var other = await CreateUserAsync(admin, "ADMIN");
        var otherClient = await LoginClientAsync(other.UserName, other.Password); // real cookie as that admin

        var response = await otherClient.PostAsync($"/api/users/{other.Id}/toggle-active", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("self-deactivate", (await Json(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_last_active_admin_cannot_be_deactivated_or_demoted()
    {
        var admin = await AdminAsync(); // test identity: it is not a database user, so the "self" rule cannot interfere
        await using (var scope = factory.Services.CreateAsyncScope()) // make "admin" the only active admin
        {
            var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();
            foreach (var u in await db.Users.Where(u => u.Role == Role.ADMIN && u.UserName != "admin").ToListAsync()) u.IsActive = false;
            await db.SaveChangesAsync();
        }
        var seeded = await Json(await admin.GetAsync("/api/users?q=admin&role=ADMIN&active=true"));
        var adminRow = seeded.GetProperty("items").EnumerateArray().Single(u => u.GetProperty("userName").GetString() == "admin");
        var id = adminRow.GetProperty("id").GetInt64();

        var deactivate = await admin.PostAsync($"/api/users/{id}/toggle-active", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, deactivate.StatusCode);
        Assert.Equal("last-admin", (await Json(deactivate)).GetProperty("code").GetString());

        var demote = await admin.PutAsJsonAsync($"/api/users/{id}",
            new { firstName = "System", lastName = "Admin", employeeNo = "10001", role = "REPORT_VIEWER" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, demote.StatusCode);
        Assert.Equal("last-admin", (await Json(demote)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task User_changes_their_own_password_and_the_old_one_stops_working()
    {
        var admin = await AdminAsync();
        var user = await CreateUserAsync(admin, "COMPONENT_TRACKER");
        var client = await LoginClientAsync(user.UserName, user.Password);

        var wrong = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "Wrong@12345", newPassword = "Fresh@12345", confirmPassword = "Fresh@12345" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.True((await Json(wrong)).GetProperty("errors").TryGetProperty("currentPassword", out _));

        var mismatch = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = user.Password, newPassword = "Fresh@12345", confirmPassword = "Other@12345" });
        Assert.True((await Json(mismatch)).GetProperty("errors").TryGetProperty("confirmPassword", out _));

        var ok = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = user.Password, newPassword = "Fresh@12345", confirmPassword = "Fresh@12345" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode); // still signed in (fresh cookie)

        var oldLogin = await ApiTestHelpers.LoginAsync(await ApiTestHelpers.CreateClientAsync(factory), user.UserName, user.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        await LoginClientAsync(user.UserName, "Fresh@12345");
    }

    [Fact]
    public async Task Every_role_can_change_their_own_password()
    {
        var admin = await AdminAsync();
        foreach (var role in Enum.GetValues<Role>().Select(r => r.ToString()))
        {
            var user = await CreateUserAsync(admin, role);
            var client = await LoginClientAsync(user.UserName, user.Password);
            var response = await client.PostAsJsonAsync("/api/auth/change-password",
                new { currentPassword = user.Password, newPassword = "Fresh@12345", confirmPassword = "Fresh@12345" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Admin_reset_password_replaces_the_old_one_and_ends_old_sessions()
    {
        var admin = await AdminAsync();
        var user = await CreateUserAsync(admin, "DELIVERY_USER");
        var session = await LoginClientAsync(user.UserName, user.Password);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/users/{user.Id}/reset-password", new { })).StatusCode);
        var weak = await admin.PostAsJsonAsync($"/api/users/{user.Id}/reset-password", new { newPassword = "weak" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, weak.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.PostAsJsonAsync($"/api/users/{user.Id}/reset-password", new { newPassword = "Reset@12345" })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/auth/me")).StatusCode);
        var old = await ApiTestHelpers.LoginAsync(await ApiTestHelpers.CreateClientAsync(factory), user.UserName, user.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        await LoginClientAsync(user.UserName, "Reset@12345");
    }
}
