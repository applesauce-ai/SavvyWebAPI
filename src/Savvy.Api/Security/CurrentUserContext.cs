using System.Security.Claims;
using Savvy.Application.Common;

namespace Savvy.Api.Security;

/// <summary>
/// Reads the authenticated caller from the current request's <see cref="ClaimsPrincipal"/>.
/// Claims are produced by the JWT issued at login (Section 7); in tests they are supplied by
/// a test authentication handler. The shape consumed here is identical in both cases.
/// </summary>
public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly ClaimsPrincipal? _user;

    public CurrentUserContext(IHttpContextAccessor accessor)
    {
        _user = accessor.HttpContext?.User;
    }

    public bool IsAuthenticated => _user?.Identity?.IsAuthenticated ?? false;

    public int UserId =>
        int.TryParse(_user?.FindFirstValue(SavvyClaimTypes.UserId), out var id) ? id : 0;

    public Guid PublicId
    {
        get
        {
            var raw = _user?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? _user?.FindFirstValue("sub");
            return Guid.TryParse(raw, out var g) ? g : Guid.Empty;
        }
    }

    public string Role => _user?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public int? PracticeId =>
        int.TryParse(_user?.FindFirstValue(SavvyClaimTypes.PracticeId), out var pid) ? pid : null;

    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.Ordinal);
}
