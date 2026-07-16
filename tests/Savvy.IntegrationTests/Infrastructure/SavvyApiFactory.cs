using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Savvy.Infrastructure.Persistence;

namespace Savvy.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the API for integration tests with an isolated SQLite in-memory database (so the real
/// SavvyDb is never touched). Runs in the "Testing" environment (skips the Development-only
/// migrate/seed bootstrap) and supplies JWT settings that the mock vault would otherwise provide.
///
/// By default the caller identity is provided by <see cref="TestAuthHandler"/> (fast, no tokens).
/// Pass <c>useTestAuth: false</c> to exercise the real JWT bearer pipeline end-to-end.
/// </summary>
public sealed class SavvyApiFactory : WebApplicationFactory<Program>
{
    // A fixed >= 32-char signing key so issued and validated tokens agree in tests.
    public const string TestSigningKey = "integration-test-signing-key-0123456789ABCDEF";

    private readonly bool _useTestAuth;
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SavvyApiFactory(bool useTestAuth = true)
    {
        _useTestAuth = useTestAuth;

        // The signing-key secret is normally supplied by the (mock) vault, which isn't loaded in
        // the Testing environment. Provide it via an environment variable so it is present when
        // CreateBuilder composes configuration — before AddSavvyJwtAuth reads it. Issuer/Audience/
        // ExpiryMinutes come from the committed appsettings.json.
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestSigningKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Replace the SQL Server context with SQLite in-memory.
            services.RemoveAll<DbContextOptions<SavvyDbContext>>();
            services.RemoveAll<SavvyDbContext>();

            _connection.Open();
            services.AddDbContext<SavvyDbContext>(options => options.UseSqlite(_connection));

            if (_useTestAuth)
            {
                // Make the header-driven test scheme the default so [Authorize] uses it.
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            }
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SavvyDbContext>();
        db.Database.EnsureCreated();
        SavvySeeder.SeedAsync(db).GetAwaiter().GetResult();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
