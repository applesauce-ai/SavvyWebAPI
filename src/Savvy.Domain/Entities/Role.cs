namespace Savvy.Domain.Entities;

/// <summary>
/// Application role (Admin, PracticeManager, Clinician). Stored as data rather than
/// an enum so new roles are an insert, not a redeploy. The JWT role claim is
/// populated from <see cref="Name"/> at login so <c>[Authorize(Roles = "...")]</c> works.
/// </summary>
public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    public ICollection<User> Users { get; set; } = new List<User>();
}
