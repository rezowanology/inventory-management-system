using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ims.Tests;

/// <summary>
/// Test-only authentication: send header "X-Test-Role: ADMIN" (or any role name) to act as that role
/// without logging in. Without the header the request stays anonymous / uses the real cookie.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RoleHeader = "X-Test-Role";
    /// <summary>Optional: act as a different user id (a test that needs "not the seeded admin" - nothing may be saved as that user).</summary>
    public const string UserIdHeader = "X-Test-UserId";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var role) || string.IsNullOrWhiteSpace(role))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Request.Headers.TryGetValue(UserIdHeader, out var uid) ? uid.ToString() : "1"), // default: a REAL user id (the seeded admin, first row of a fresh ims_test): audit rows have a foreign key to users
            new Claim(ClaimTypes.Name, "test-" + role),
            new Claim(ClaimTypes.Role, role.ToString()),
        ], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
