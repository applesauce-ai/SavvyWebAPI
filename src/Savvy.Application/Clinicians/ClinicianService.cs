using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Savvy.Application.Common;
using Savvy.Domain.Entities;

namespace Savvy.Application.Clinicians;

/// <summary>Creates Clinician users scoped to a practice. Admin-only (enforced by the controller).</summary>
public class ClinicianService : IClinicianService
{
    private const string ClinicianRoleName = "Clinician";

    private readonly ISavvyDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;

    public ClinicianService(ISavvyDbContext db, IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<ClinicianResponse> CreateAsync(int practiceId, CreateClinicianRequest request, CancellationToken ct = default)
    {
        if (!await _db.Practices.AnyAsync(p => p.Id == practiceId, ct))
            throw NotFoundException.For("Practice", practiceId);

        var email = request.Email.Trim();
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException($"A user with email '{email}' already exists.");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == ClinicianRoleName, ct)
            ?? throw new ValidationException("The Clinician role is not configured.");

        var user = new User
        {
            Email = email,
            Role = role,
            PracticeId = practiceId
        };
        // Hash with the same PasswordHasher used at login — never store plaintext.
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique-email index backstop under concurrency.
            throw new ConflictException($"A user with email '{email}' already exists.");
        }

        return ClinicianResponse.From(user);
    }
}
