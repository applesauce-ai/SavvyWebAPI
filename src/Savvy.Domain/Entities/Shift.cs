using Savvy.Domain.Enums;

namespace Savvy.Domain.Entities;

/// <summary>
/// A schedulable unit of work at a practice. A clinician may be assigned at creation
/// or later (hence <see cref="ClinicianId"/> is nullable). Becomes
/// <see cref="ShiftStatus.Completed"/> when its timesheet is submitted.
/// </summary>
public class Shift
{
    public int Id { get; set; }

    public int PracticeId { get; set; }
    public Practice Practice { get; set; } = null!;

    /// <summary>Assigned clinician (a <see cref="User"/>). Null until claimed/assigned.</summary>
    public int? ClinicianId { get; set; }
    public User? Clinician { get; set; }

    /// <summary>Calendar date of the shift (date-only).</summary>
    public DateOnly Date { get; set; }

    /// <summary>Scheduled start/end, stored and compared in UTC.</summary>
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    public decimal HourlyRate { get; set; }

    /// <summary>Clinical role required for the shift (e.g. "Nurse"), distinct from the user Role table.</summary>
    public string Role { get; set; } = null!;
    public string Location { get; set; } = null!;

    public ShiftStatus Status { get; set; } = ShiftStatus.Open;

    public Timesheet? Timesheet { get; set; }
}
