using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ims.Tests;

/// <summary>Real cookie login, no test-auth shortcut.</summary>
[Collection(ApiCollection.Name)]
public class AuthFlowTests(ImsApiFactory factory)
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Login_then_me_returns_the_capability_list()
    {
        var client = await ApiTestHelpers.CreateClientAsync(factory);
        var login = await ApiTestHelpers.LoginAsync(client, "viewer", "Demo@12345");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var me = await client.GetAsync("/api/auth/me"); // the cookie keeps us signed in
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await Json(me);
        Assert.Equal("REPORT_VIEWER", body.GetProperty("role").GetString());
        var caps = body.GetProperty("capabilities").EnumerateArray().Select(c => c.GetString()).ToList();
        Assert.Contains("history.view", caps);
        Assert.Contains("reports.view", caps);
        Assert.DoesNotContain("ops.receive", caps);
    }

    [Fact]
    public async Task Admin_sees_everything_and_logout_ends_the_session()
    {
        var client = await ApiTestHelpers.CreateClientAsync(factory);
        await ApiTestHelpers.LoginAsync(client, "admin", "Admin@12345");
        var caps = (await Json(await client.GetAsync("/api/auth/me"))).GetProperty("capabilities").GetArrayLength();
        Assert.Equal(12, caps);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_401()
    {
        var client = await ApiTestHelpers.CreateClientAsync(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ApiTestHelpers.LoginAsync(client, "admin", "nope")).StatusCode);
    }

    [Fact]
    public async Task Post_without_antiforgery_token_is_rejected()
    {
        var client = factory.CreateClient(); // no token fetched
        var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = "admin", password = "Admin@12345" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Registration_creates_an_inactive_viewer_who_cannot_log_in_yet()
    {
        var client = await ApiTestHelpers.CreateClientAsync(factory);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            userName = "newbie" + suffix, password = "Newbie@12345", firstName = "New", lastName = "Bie", employeeNo = "E" + suffix
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await ApiTestHelpers.LoginAsync(client, "newbie" + suffix, "Newbie@12345");
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Equal("This account is not active", (await Json(login)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Registration_rejects_a_duplicate_employee_number()
    {
        var client = await ApiTestHelpers.CreateClientAsync(factory);
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            userName = "dup" + Guid.NewGuid().ToString("N")[..6], password = "Newbie@12345",
            firstName = "A", lastName = "B", employeeNo = "10001" // the seeded admin's number
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("employeeNo", out _));
    }

    [Fact]
    public async Task History_and_dashboard_show_the_seeded_sample()
    {
        var admin = await ApiTestHelpers.CreateClientAsync(factory, "ADMIN");
        var history = await Json(await admin.GetAsync("/api/history?q=2440701AK2057"));
        Assert.Equal(3, history.GetProperty("totalCount").GetInt32());
        // newest first: DELIVERED, RECEIVED, COMPONENT_TRACKED
        var events = history.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("eventType").GetString()).ToList();
        Assert.Equal(["DELIVERED", "RECEIVED", "COMPONENT_TRACKED"], events);

        var dash = await Json(await admin.GetAsync("/api/dashboard"));
        Assert.True(dash.GetProperty("totalUnits").GetInt32() >= 1); // other tests add units to the shared test database
        Assert.True(dash.GetProperty("stockCount").GetInt32() >= 0);
    }
}
