using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Savvy.Application.Auth;
using Savvy.Application.Common;
using Savvy.Domain.Entities;

namespace Savvy.Api.Security;

/// <summary>Issues signed HS256 JWTs carrying the claims consumed by <see cref="CurrentUserContext"/>.</summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings) => _settings = settings.Value;

    public TokenResult CreateToken(User user)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_settings.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.PublicId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(SavvyClaimTypes.UserId, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role.Name),
            new(ClaimTypes.Email, user.Email)
        };

        if (user.PracticeId is int practiceId)
            claims.Add(new Claim(SavvyClaimTypes.PracticeId, practiceId.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new TokenResult(accessToken, expires);
    }
}
