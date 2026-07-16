using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Savvy.Application.Common;

namespace Savvy.IntegrationTests.Infrastructure;

/// <summary>
/// Test authentication scheme. Reads the caller identity from request headers so each test can
/// act as a specific role/practice without issuing real JWTs:
///   X-Test-Role      → role name (Admin | PracticeManager | Clinician)
///   X-Test-PracticeId → practice id (omit for Admin)
///   X-Test-Uid       → internal user id
///   X-Test-Sub       → public id (GUID)
/// A request with no X-Test-Role header is left unauthenticated (→ 401).
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var role) || string.IsNullOrWhiteSpace(role))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role.ToString()),
            new(SavvyClaimTypes.UserId, Request.Headers["X-Test-Uid"].FirstOrDefault() ?? "0"),
            new(ClaimTypes.NameIdentifier, Request.Headers["X-Test-Sub"].FirstOrDefault() ?? Guid.Empty.ToString())
        };

        var practiceId = Request.Headers["X-Test-PracticeId"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(practiceId))
            claims.Add(new Claim(SavvyClaimTypes.PracticeId, practiceId));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
