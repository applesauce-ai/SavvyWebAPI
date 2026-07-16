using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Savvy.Application.Common;
using Savvy.Domain.Entities;

namespace Savvy.Application.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly ISavvyDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthService(ISavvyDbContext db, IPasswordHasher<User> passwordHasher, ITokenService tokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        // Uniform failure regardless of whether the email exists or the password is wrong,
        // so the endpoint doesn't reveal which accounts are registered.
        if (user is null || !PasswordIsValid(user, request.Password))
            throw new UnauthorizedException("Invalid email or password.");

        var token = _tokenService.CreateToken(user);

        return new LoginResponse
        {
            AccessToken = token.AccessToken,
            ExpiresAtUtc = token.ExpiresAtUtc,
            Role = user.Role.Name,
            PublicId = user.PublicId
        };
    }

    private bool PasswordIsValid(User user, string password)
        => _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password)
           is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
