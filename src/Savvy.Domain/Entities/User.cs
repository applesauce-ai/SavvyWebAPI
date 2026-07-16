namespace Savvy.Domain.Entities;

/// <summary>
/// An authenticated principal. <see cref="Id"/> is the internal key used for FK joins;
/// <see cref="PublicId"/> is the non-guessable external identifier used as the JWT
/// <c>sub</c> claim. <see cref="PracticeId"/> is null for Admin (not practice-scoped).
/// </summary>
public class User
{
    public int Id { get; set; }

    /// <summary>Stable external identifier (JWT subject). Never a sequential int.</summary>
    public Guid PublicId { get; set; } = Guid.NewGuid();

    public string Email { get; set; } = null!;

    /// <summary>PBKDF2 hash produced by ASP.NET Core Identity's PasswordHasher. Never plaintext.</summary>
    public string PasswordHash { get; set; } = null!;

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    /// <summary>Null for Admin; set for PracticeManager/Clinician.</summary>
    public int? PracticeId { get; set; }
    public Practice? Practice { get; set; }

    /// <summary>Shifts assigned to this user when acting as a Clinician.</summary>
    public ICollection<Shift> AssignedShifts { get; set; } = new List<Shift>();
}
