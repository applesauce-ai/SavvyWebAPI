using Microsoft.Extensions.Configuration;
using Savvy.Api.Configuration;
using Xunit;

namespace Savvy.IntegrationTests;

/// <summary>
/// Verifies the mock Key Vault provider in isolation (no app boot, no SQL Server, no real secret
/// file — so it runs anywhere, including CI): a secret named "ConnectionStrings--SavvyDb" is
/// translated to the config key "ConnectionStrings:SavvyDb" and, being added last, overrides an
/// earlier configuration source.
/// </summary>
public class MockKeyVaultConfigurationTests
{
    [Fact]
    public void Mock_vault_loads_translates_double_dash_and_overrides_earlier_sources()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kv-mock-{Guid.NewGuid():N}.json");
        File.WriteAllText(path,
            "{ \"ConnectionStrings--SavvyDb\": \"Server=localhost;Database=SavvyDb;User Id=savvy_app;Password=x\" }");

        try
        {
            var config = new ConfigurationBuilder()
                // Earlier (lower-precedence) source, as appsettings.json would be.
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SavvyDb"] = "Trusted_Connection=True"
                })
                // The mock vault, added last (highest precedence) — mirrors Program.cs.
                .Add(new MockKeyVaultConfigurationSource { FilePath = path, Optional = true })
                .Build();

            var connectionString = config.GetConnectionString("SavvyDb");

            // "ConnectionStrings--SavvyDb" was translated to "ConnectionStrings:SavvyDb" ...
            Assert.Contains("User Id=savvy_app", connectionString);
            // ... and overrode the earlier in-memory (Windows-auth) default.
            Assert.DoesNotContain("Trusted_Connection=True", connectionString);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
