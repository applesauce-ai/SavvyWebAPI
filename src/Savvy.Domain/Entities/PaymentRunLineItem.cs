namespace Savvy.Domain.Entities;

/// <summary>
/// One timesheet's contribution to a payment run. Amounts are a persisted snapshot of the
/// calculation (hours x rate, fee, net) so the run remains reproducible and auditable.
/// Only ever read as part of its parent run, so it has no external PublicId.
/// </summary>
public class PaymentRunLineItem
{
    public int Id { get; set; }

    public int PaymentRunId { get; set; }
    public PaymentRun PaymentRun { get; set; } = null!;

    public int TimesheetId { get; set; }
    public Timesheet Timesheet { get; set; } = null!;

    public int ClinicianId { get; set; }
    public User Clinician { get; set; } = null!;

    public decimal Hours { get; set; }
    public decimal Rate { get; set; }
    public decimal Gross { get; set; }
    public decimal Fee { get; set; }
    public decimal Net { get; set; }
}
