using Ims.Api.Data;
using Ims.Api.Data.Seed;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Ims.Tests;

/// <summary>
/// Starts the real API in memory (WebApplicationFactory) against a REAL PostgreSQL database named "ims_test".
/// The database is dropped, re-created, migrated and seeded once per test run. It never touches "ims".
/// The connection details come from the same user-secret as the app (host, user and password are reused;
/// only the database name is swapped), so no password is ever written in the tests.
/// </summary>
public class ImsApiFactory : WebApplicationFactory<Program>
{
    public const string TestDatabase = "ims_test";
    private static readonly object Gate = new();
    private static bool _databaseReady;

    private static string BuildTestConnectionString(out string adminConnectionString)
    {
        var config = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        var original = config["ConnectionStrings:Ims"]
            ?? throw new InvalidOperationException("Set the ConnectionStrings:Ims user-secret first (see README).");
        adminConnectionString = original;
        var test = new NpgsqlConnectionStringBuilder(original) { Database = TestDatabase };
        return test.ConnectionString;
    }

    /// <summary>Drop and re-create ims_test, then apply the EF migrations. Runs once per process.</summary>
    private static void PrepareDatabase(string testConnection, string adminConnection)
    {
        lock (Gate)
        {
            if (_databaseReady) return;
            var name = new NpgsqlConnectionStringBuilder(testConnection).Database;
            if (name != TestDatabase) throw new InvalidOperationException("Refusing to run tests on database " + name);

            NpgsqlConnection.ClearAllPools();
            using (var admin = new NpgsqlConnection(adminConnection)) // connected to the normal db; only used to DROP/CREATE ims_test
            {
                admin.Open();
                foreach (var sql in new[] { $"DROP DATABASE IF EXISTS {TestDatabase} WITH (FORCE)", $"CREATE DATABASE {TestDatabase}" })
                {
                    using var cmd = new NpgsqlCommand(sql, admin);
                    cmd.ExecuteNonQuery();
                }
            }

            var options = new DbContextOptionsBuilder<ImsDbContext>()
                .UseNpgsql(testConnection).UseSnakeCaseNamingConvention().Options;
            using (var db = new ImsDbContext(options)) db.Database.Migrate();
            _databaseReady = true;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var testConnection = BuildTestConnectionString(out var adminConnection);
        PrepareDatabase(testConnection, adminConnection);

        builder.UseEnvironment("Development");
        // Last configuration source wins: point the app at ims_test.
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Ims"] = testConnection }));

        builder.ConfigureServices(services =>
        {
            // "Test" authentication scheme: a request with the X-Test-Role header is signed in as that role.
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            // Requests WITHOUT the header still use the real Identity cookie (for the real login tests).
            services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, o =>
                o.ForwardDefaultSelector = ctx =>
                    ctx.Request.Headers.ContainsKey(TestAuthHandler.RoleHeader) ? TestAuthHandler.SchemeName : null);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        DbSeeder.SeedAsync(scope.ServiceProvider).GetAwaiter().GetResult(); // idempotent
        return host;
    }
}

/// <summary>One shared factory for all test classes (they run one after another, sharing the ims_test database).</summary>
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ImsApiFactory>
{
    public const string Name = "api";
}
