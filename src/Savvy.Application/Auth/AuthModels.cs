using System.ComponentModel.DataAnnotations;
using Savvy.Domain.Entities;

namespace Savvy.Application.Auth;

public record LoginRequest
{
    [Required, EmailAddress] public string Email { get; init; } = null!;
    [Required] public string Password { get; init; } = null!;
}

public record LoginResponse
{
    public string AccessToken { get; init; } = null!;
    public DateTime ExpiresAtUtc { get; init; }
    public string Role { get; init; } = null!;
    public Guid PublicId { get; init; }
}

/// <summary>Result of minting a token for a user.</summary>
public record TokenResult(string AccessToken, DateTime ExpiresAtUtc);

/// <summary>Issues signed access tokens. Implemented in the API layer (JWT-specific).</summary>
public interface ITokenService
{
    TokenResult CreateToken(User user);
}
