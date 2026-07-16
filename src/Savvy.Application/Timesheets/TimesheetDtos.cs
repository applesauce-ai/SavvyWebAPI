using System.ComponentModel.DataAnnotations;
using Savvy.Domain.Calculations;
using Savvy.Domain.Entities;

namespace Savvy.Application.Timesheets;

/// <summary>Timesheet as returned by the API. Hours are computed from the worked period.</summary>
public record TimesheetResponse
{
    public Guid PublicId { get; init; }
    public int ShiftId { get; init; }
    public int ClinicianId { get; init; }
    public DateTime WorkedStartUtc { get; init; }
    public DateTime WorkedEndUtc { get; init; }
    public int UnpaidBreakMinutes { get; init; }
    public decimal Hours { get; init; }
    public string? Notes { get; init; }
    public string BusinessReference { get; init; } = null!;
    public DateTime CreatedAtUtc { get; init; }

    public static TimesheetResponse From(Timesheet t) => new()
    {
        PublicId = t.PublicId,
        ShiftId = t.ShiftId,
        ClinicianId = t.ClinicianId,
        WorkedStartUtc = t.WorkedStartUtc,
        WorkedEndUtc = t.WorkedEndUtc,
        UnpaidBreakMinutes = t.UnpaidBreakMinutes,
        Hours = WorkHours.ComputeHours(t.WorkedStartUtc, t.WorkedEndUtc, t.UnpaidBreakMinutes),
        Notes = t.Notes,
        BusinessReference = t.BusinessReference,
        CreatedAtUtc = t.CreatedAtUtc
    };
}

/// <summary>Submit a timesheet against a shift. Idempotent on <see cref="BusinessReference"/>.</summary>
public record SubmitTimesheetRequest
{
    [Required] public DateTime WorkedStartUtc { get; init; }
    [Required] public DateTime WorkedEndUtc { get; init; }

    [Range(0, 1440)] public int UnpaidBreakMinutes { get; init; }

    [StringLength(1000)] public string? Notes { get; init; }

    [Required, StringLength(100)] public string BusinessReference { get; init; } = null!;
}

/// <summary>Result of a submission: the timesheet plus whether it was newly created (201) or
/// an idempotent replay of an existing one (200).</summary>
public record TimesheetSubmissionResult(TimesheetResponse Timesheet, bool Created);
