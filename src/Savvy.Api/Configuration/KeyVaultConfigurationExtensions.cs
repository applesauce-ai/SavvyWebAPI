using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Savvy.Api.Configuration;

/// <summary>
/// Wires the application's secret store. Precedence:
///   1. If <c>KeyVault:Uri</c> is configured (e.g. set by Azure App Service in
///      Production) → the real Azure Key Vault, authenticated with
///      <see cref="DefaultAzureCredential"/> (Managed Identity in Azure, developer
///      credentials locally). No secrets are stored on the host.
///   2. Else if <c>KeyVault:UseMock</c> is true (local dev) → a mock vault backed
///      by <c>keyvault.mock.json</c> that behaves like Key Vault.
///   3. Else → no vault source is added and lower-precedence configuration
///      (appsettings, user-secrets, environment variables) supplies values.
/// The vault source is added LAST so it overrides other configuration sources.
/// </summary>
public static class KeyVaultConfigurationExtensions
{
    public const string MockFileName = "keyvault.mock.json";

    public static ConfigurationManager AddSavvyKeyVault(
        this ConfigurationManager configuration,
        IHostEnvironment environment)
    {
        var vaultUri = configuration["KeyVault:Uri"];

        if (!string.IsNullOrWhiteSpace(vaultUri))
        {
            // Production path — real Azure Key Vault via Managed Identity.
            configuration.AddAzureKeyVault(new Uri(vaultUri), new DefaultAzureCredential());
            return configuration;
        }

        var useMock = configuration.GetValue<bool>("KeyVault:UseMock");
        if (useMock)
        {
            var mockPath = Path.Combine(environment.ContentRootPath, MockFileName);
            // ConfigurationManager implements IConfigurationBuilder explicitly.
            ((IConfigurationBuilder)configuration).Add(new MockKeyVaultConfigurationSource
            {
                FilePath = mockPath,
                Optional = true
            });
        }

        return configuration;
    }
}
