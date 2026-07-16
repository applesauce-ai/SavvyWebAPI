namespace Savvy.Domain.Entities;

/// <summary>
/// A clinician's record of hours worked against a single shift. Exactly one timesheet
/// per shift (enforced by a unique index on <see cref="ShiftId"/>).
/// <see cref="BusinessReference"/> is the idempotency key for submission.
/// </summary>
public class Timesheet
{
    public int Id { get; set; }

    /// <summary>Non-guessable external identifier used in API routes.</summary>
    public Guid PublicId { get; set; } = Guid.NewGuid();

    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;

    public int ClinicianId { get; set; }
    public User Clinician { get; set; } = null!;

    /// <summary>Actual worked start/end, in UTC.</summary>
    public DateTime WorkedStartUtc { get; set; }
    public DateTime WorkedEndUtc { get; set; }

    public int UnpaidBreakMinutes { get; set; }

    public string? Notes { get; set; }

    /// <summary>Caller-supplied idempotency key. Unique across timesheets.</summary>
    public string BusinessReference { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<PaymentRunLineItem> LineItems { get; set; } = new List<PaymentRunLineItem>();
}
