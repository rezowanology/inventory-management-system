using System.Net;
using System.Net.Http.Json;
using Ims.Api.Authorization;
using Ims.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Ims.Tests;

/// <summary>
/// Walks EVERY /api endpoint the app really exposes (read from the routing table, so a new endpoint cannot be
/// forgotten) and checks it against the permission matrix for all 5 roles plus anonymous.
/// </summary>
[Collection(ApiCollection.Name)]
public class PolicyMatrixTests(ImsApiFactory factory)
{
    private record Ep(string Method, string Url, string? Policy, bool AllowAnonymous);

    private IEnumerable<Ep> Endpoints() =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("api/"))
            .SelectMany(e =>
            {
                var url = "/" + System.Text.RegularExpressions.Regex.Replace(e.RoutePattern.RawText!, @"\{id[^}]*\}", "1");
                var policy = e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).FirstOrDefault(p => p != null);
                var anon = e.Metadata.GetMetadata<IAllowAnonymous>() != null;
                var methods = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
                return methods.Select(m => new Ep(m, url, policy, anon));
            });

    private static HttpRequestMessage Request(Ep ep) => new(new HttpMethod(ep.Method), ep.Url)
    {
        Content = ep.Method is "POST" or "PUT" ? JsonContent.Create(new { }) : null
    };

    [Fact]
    public void Every_api_endpoint_has_a_policy_or_is_explicitly_anonymous_or_needs_login()
    {
        var eps = Endpoints().ToList();
        Assert.True(eps.Count >= 40, $"expected the whole API contract, found {eps.Count} endpoints");
        // Endpoints with neither policy nor AllowAnonymous are protected by the FallbackPolicy (signed-in only): only /me.
        var onlyFallback = eps.Where(e => e.Policy is null && !e.AllowAnonymous).Select(e => e.Url).Distinct().ToList();
        Assert.Equal(["/api/auth/me"], onlyFallback);
    }

    [Fact]
    public async Task Anonymous_gets_401_on_every_protected_endpoint()
    {
        var anon = await ApiTestHelpers.CreateClientAsync(factory);
        var failures = new List<string>();
        foreach (var ep in Endpoints().Where(e => !e.AllowAnonymous))
        {
            var response = await anon.SendAsync(Request(ep));
            if (response.StatusCode != HttpStatusCode.Unauthorized) failures.Add($"{ep.Method} {ep.Url} -> {(int)response.StatusCode}");
        }
        Assert.Empty(failures);
    }

    [Fact]
    public async Task Each_role_gets_403_exactly_where_the_matrix_says_no()
    {
        var failures = new List<string>();
        foreach (var role in Enum.GetValues<Role>())
        {
            var client = await ApiTestHelpers.CreateClientAsync(factory, role.ToString());
            foreach (var ep in Endpoints().Where(e => e.Policy is not null))
            {
                var allowed = Capabilities.Matrix[ep.Policy!].Contains(role);
                var status = (await client.SendAsync(Request(ep))).StatusCode;
                var ok = allowed ? status is not (HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                                 : status == HttpStatusCode.Forbidden;
                if (!ok) failures.Add($"{role} {ep.Method} {ep.Url} ({ep.Policy}) allowed={allowed} -> {(int)status}");
            }
        }
        Assert.Empty(failures);
    }

    [Fact]
    public async Task Viewer_calling_receive_directly_gets_403()
    {
        var viewer = await ApiTestHelpers.CreateClientAsync(factory, "REPORT_VIEWER");
        var response = await viewer.PostAsJsonAsync("/api/ops/receive", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
