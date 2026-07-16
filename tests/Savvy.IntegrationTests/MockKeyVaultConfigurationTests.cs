using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Savvy.IntegrationTests;

/// <summary>
/// Verifies the mock Azure Key Vault wiring: in the Development environment the
/// secret stored in keyvault.mock.json (secret name "ConnectionStrings--SavvyDb")
/// is translated to the config key "ConnectionStrings:SavvyDb" and overrides the
/// non-secret default in appsettings.json.
/// </summary>
public class MockKeyVaultConfigurationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MockKeyVaultConfigurationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
    }

    [Fact]
    public void MockVault_supplies_and_overrides_the_SavvyDb_connection_string()
    {
        // Force the host to build so configuration is composed.
        using var scope = _factory.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var connectionString = config.GetConnectionString("SavvyDb");

        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        // The mock vault holds the SQL-auth string; the appsettings default is Windows auth.
        // Seeing the SQL login proves the mock vault loaded AND won precedence.
        Assert.Contains("User Id=savvy_app", connectionString);
        Assert.DoesNotContain("Trusted_Connection=True", connectionString);
    }
}
