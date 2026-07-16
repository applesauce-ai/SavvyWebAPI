using System.ComponentModel.DataAnnotations;

namespace Savvy.Api.Security;

/// <summary>
/// JWT configuration. Issuer/Audience/ExpiryMinutes are non-secret (appsettings); SigningKey is
/// a secret supplied by the vault (mock Key Vault in dev, Azure Key Vault in production).
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; init; } = null!;
    [Required] public string Audience { get; init; } = null!;

    [Required, MinLength(32)] // >= 256 bits for HS256
    public string SigningKey { get; init; } = null!;

    [Range(1, 1440)] public int ExpiryMinutes { get; init; } = 60;
}
