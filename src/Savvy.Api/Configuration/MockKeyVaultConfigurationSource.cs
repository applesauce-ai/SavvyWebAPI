using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Savvy.Api.Configuration;

/// <summary>
/// Local stand-in for Azure Key Vault used during development when no real vault
/// is available. Secrets are read from a flat JSON file (mirroring how Key Vault
/// exposes a flat list of secret name/value pairs) and the same name translation
/// Azure applies is performed here: a secret named <c>ConnectionStrings--SavvyDb</c>
/// becomes the configuration key <c>ConnectionStrings:SavvyDb</c>.
///
/// This is a MOCK. In Production the real <c>AddAzureKeyVault</c> provider is used
/// instead (see <see cref="KeyVaultConfigurationExtensions"/>), and the application
/// code consuming the secret does not change.
/// </summary>
public sealed class MockKeyVaultConfigurationSource : IConfigurationSource
{
    public required string FilePath { get; init; }
    public bool Optional { get; init; } = true;

    public IConfigurationProvider Build(IConfigurationBuilder builder)
        => new MockKeyVaultConfigurationProvider(this);
}

public sealed class MockKeyVaultConfigurationProvider : ConfigurationProvider
{
    // Azure Key Vault's documented separator for hierarchical secret names.
    private const string KeyVaultSeparator = "--";

    private readonly MockKeyVaultConfigurationSource _source;

    public MockKeyVaultConfigurationProvider(MockKeyVaultConfigurationSource source)
        => _source = source;

    public override void Load()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(_source.FilePath))
        {
            if (!_source.Optional)
            {
                throw new FileNotFoundException(
                    $"Mock Key Vault file not found: {_source.FilePath}");
            }

            Data = data;
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(_source.FilePath));

        foreach (var secret in document.RootElement.EnumerateObject())
        {
            // Emulate Azure Key Vault's "--" => ":" translation for section nesting.
            var key = secret.Name.Replace(KeyVaultSeparator, ConfigurationPath.KeyDelimiter);
            data[key] = secret.Value.ValueKind == JsonValueKind.String
                ? secret.Value.GetString()
                : secret.Value.GetRawText();
        }

        Data = data;
    }
}
