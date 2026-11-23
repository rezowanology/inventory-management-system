using Ims.Api.Authorization;
using Ims.Api.Data;
using Ims.Api.Data.Seed;
using Ims.Api.Domain;
using Ims.Api.Infrastructure;
using Ims.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Program.cs is the "composition root": it registers services (dependency injection) and builds the HTTP pipeline.
var builder = WebApplication.CreateBuilder(args);

// ---- Secrets: the connection string lives ONLY in .NET user-secrets, never in files that git tracks ----
if (builder.Environment.IsDevelopment())
    builder.Configuration.AddUserSecrets<Program>(optional: true);

if (string.IsNullOrWhiteSpace(builder.Configuration["ConnectionStrings:Ims"]))
    throw new InvalidOperationException(
        "The database connection string is missing. Store it once as a user-secret by running:\n" +
        "dotnet user-secrets set \"ConnectionStrings:Ims\" " +
        "\"Host=localhost;Port=5432;Database=ims;Username=ims_user;Password=<your ims_user password>\" " +
        "--project src/Ims.Api");

// ---- Database (EF Core + Npgsql, snake_case names) ----
builder.Services.AddSingleton<AppendOnlyInterceptor>(); // rule R6, first line of defence
builder.Services.AddDbContext<ImsDbContext>((sp, options) => options
    // Read lazily (inside this callback) so the integration tests can point it at the ims_test database.
    .UseNpgsql(sp.GetRequiredService<IConfiguration>()["ConnectionStrings:Ims"])
    .UseSnakeCaseNamingConvention()
    .AddInterceptors(sp.GetRequiredService<AppendOnlyInterceptor>()));

// ---- Identity: users, password hashing, cookie sign-in. No Identity role tables: the role is a column. ----
builder.Services
    .AddIdentityCore<ApplicationUser>(o =>
    {
        o.Password.RequiredLength = 8;
        o.User.RequireUniqueEmail = false;
    })
    .AddEntityFrameworkStores<ImsDbContext>()
    .AddSignInManager<ImsSignInManager>()
    .AddDefaultTokenProviders() // needed by the admin "reset password" flow (one-time reset token)
    .AddClaimsPrincipalFactory<ImsClaimsPrincipalFactory>();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "ims.auth";
    o.Cookie.HttpOnly = true;                  // JavaScript cannot read the login cookie
    o.Cookie.SameSite = SameSiteMode.Strict;   // not sent on cross-site requests
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    // For API calls answer 401/403 instead of redirecting to a login page.
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});

// Re-check the security stamp on EVERY request so deactivated users are logged out immediately (R12).
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);

// ---- Authorization: one policy per capability (see Authorization/Capabilities.cs) ----
builder.Services.AddAuthorization(o =>
{
    foreach (var (capability, roles) in Capabilities.Matrix)
    {
        var roleNames = roles.Select(r => r.ToString()).ToArray();
        o.AddPolicy(capability, p => p.RequireAuthenticatedUser().RequireRole(roleNames));
    }
    // Anything without an explicit attribute still requires a signed-in user.
    o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

// ---- Antiforgery: header "X-XSRF-TOKEN" checked on every unsafe method (POST/PUT/DELETE) ----
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-XSRF-TOKEN";
    o.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddControllers(o => o.Filters.Add<ValidateAntiforgeryFilter>())
    // Send enums as text ("ADMIN") in JSON.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// ---- Errors as RFC 7807 ProblemDetails ----
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// ---- Our own services (scoped = one instance per HTTP request) ----
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<MasterDataService>(); // products / components / points
builder.Services.AddScoped<UserAdminService>();  // user administration via UserManager
builder.Services.AddSingleton<AppTime>();        // display time zone helper (A12)
builder.Services.AddScoped<ExportService>();      // CSV / Excel downloads
builder.Services.AddScoped<ReportService>();      // stock, summary and barcode reports

var app = builder.Build();

// "seed" command: seed the database and exit (needs "dotnet ef database update" first).
if (args.Contains("seed"))
{
    using var scope = app.Services.CreateScope();
    await DbSeeder.SeedAsync(scope.ServiceProvider);
    Console.WriteLine("Seeding finished.");
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages(); // plain ProblemDetails bodies for 401/403/404 without a body
app.UseDefaultFiles();
app.UseStaticFiles();     // serves the built React app from wwwroot (demo mode)
app.UseRouting();         // explicit, so static files are tried BEFORE the SPA fallback endpoint matches
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// Any non-API URL falls back to the React app so client-side routes survive a browser refresh.
app.MapFallbackToFile("{*path:regex(^(?!api/).*$)}", "index.html").AllowAnonymous();

app.Run();

// Lets the test project reference Program (WebApplicationFactory<Program>).
public partial class Program { }
