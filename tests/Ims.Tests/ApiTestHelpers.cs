using System.Net.Http.Json;

namespace Ims.Tests;

public static class ApiTestHelpers
{
    /// <summary>
    /// Creates a client (with its own cookie jar) and fetches an antiforgery token, the same way the React app does:
    /// GET /api/auth/csrf, then send the XSRF-TOKEN cookie value back in the X-XSRF-TOKEN header.
    /// </summary>
    public static async Task<HttpClient> CreateClientAsync(ImsApiFactory factory, string? testRole = null)
    {
        var client = factory.CreateClient(new() { HandleCookies = true, AllowAutoRedirect = false });
        if (testRole is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, testRole);
        await RefreshCsrfAsync(client);
        return client;
    }

    public static async Task RefreshCsrfAsync(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        var setCookie = response.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("XSRF-TOKEN="));
        var raw = setCookie["XSRF-TOKEN=".Length..].Split(';')[0];
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", Uri.UnescapeDataString(raw));
    }

    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string user, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = user, password });
        if (response.IsSuccessStatusCode) await RefreshCsrfAsync(client); // identity changed -> new token
        return response;
    }
}
