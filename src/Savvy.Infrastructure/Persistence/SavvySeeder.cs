using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Savvy.Domain.Entities;
using Savvy.Domain.Enums;

namespace Savvy.Infrastructure.Persistence;

/// <summary>
/// Seeds baseline / demo data: 3 roles, 1 practice, 3 users (one per role) and a set of
/// shifts. Idempotent — running it against an already-seeded database is a no-op.
///
/// Deterministic <c>PublicId</c> GUIDs and known demo passwords are used so the data is
/// reproducible for manual testing and integration tests. The demo passwords are for local
/// development only (documented in SOLUTION.md); production users are provisioned separately.
/// </summary>
public static class SavvySeeder
{
    // --- Well-known demo credentials (local/dev only) ---
    public const string AdminEmail = "admin@savvy.test";
    public const string ManagerEmail = "manager@savvy.test";
    public const string ClinicianEmail = "clinician@savvy.test";

    public const string AdminPassword = "Admin#12345";
    public const string ManagerPassword = "Manager#12345";
    public const string ClinicianPassword = "Clinician#12345";

    // Deterministic external identifiers.
    public static readonly Guid AdminPublicId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ManagerPublicId = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid ClinicianPublicId = new("33333333-3333-3333-3333-333333333333");

    public static async Task SeedAsync(SavvyDbContext db, CancellationToken ct = default)
    {
        // Idempotency guard: if roles exist we assume the baseline is already present.
        if (await db.Roles.AnyAsync(ct))
        {
            return;
        }

        var hasher = new PasswordHasher<User>();

        // Roles
        var adminRole = new Role { Name = "Admin" };
        var managerRole = new Role { Name = "PracticeManager" };
        var clinicianRole = new Role { Name = "Clinician" };

        // Practice
        var practice = new Practice { Name = "Savvy Medical Practice" };

        // Users (Admin is not practice-scoped; Manager/Clinician belong to the practice)
        var admin = new User
        {
            PublicId = AdminPublicId,
            Email = AdminEmail,
            Role = adminRole,
            Practice = null
        };
        admin.PasswordHash = hasher.HashPassword(admin, AdminPassword);

        var manager = new User
        {
            PublicId = ManagerPublicId,
            Email = ManagerEmail,
            Role = managerRole,
            Practice = practice
        };
        manager.PasswordHash = hasher.HashPassword(manager, ManagerPassword);

        var clinician = new User
        {
            PublicId = ClinicianPublicId,
            Email = ClinicianEmail,
            Role = clinicianRole,
            Practice = practice
        };
        clinician.PasswordHash = hasher.HashPassword(clinician, ClinicianPassword);

        // Shifts
        // S1: assigned to the clinician, in the past -> ready to be timesheeted.
        var readyToTimesheet = new Shift
        {
            Practice = practice,
            Clinician = clinician,
            Date = new DateOnly(2026, 7, 14),
            StartUtc = new DateTime(2026, 7, 14, 8, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2026, 7, 14, 16, 0, 0, DateTimeKind.Utc),
            HourlyRate = 25.00m,
            Role = "Nurse",
            Location = "Main Ward",
            Status = ShiftStatus.Open
        };

        // S2: open and unassigned (not yet claimed by a clinician).
        var openUnassigned = new Shift
        {
            Practice = practice,
            Clinician = null,
            Date = new DateOnly(2026, 7, 16),
            StartUtc = new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2026, 7, 16, 16, 0, 0, DateTimeKind.Utc),
            HourlyRate = 25.00m,
            Role = "Nurse",
            Location = "Main Ward",
            Status = ShiftStatus.Open
        };

        // S3: assigned to the clinician, in the future.
        var futureAssigned = new Shift
        {
            Practice = practice,
            Clinician = clinician,
            Date = new DateOnly(2026, 7, 18),
            StartUtc = new DateTime(2026, 7, 18, 9, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2026, 7, 18, 17, 0, 0, DateTimeKind.Utc),
            HourlyRate = 30.00m,
            Role = "Doctor",
            Location = "Clinic A",
            Status = ShiftStatus.Open
        };

        db.AddRange(adminRole, managerRole, clinicianRole);
        db.Add(practice);
        db.AddRange(admin, manager, clinician);
        db.AddRange(readyToTimesheet, openUnassigned, futureAssigned);

        await db.SaveChangesAsync(ct);
    }
}
