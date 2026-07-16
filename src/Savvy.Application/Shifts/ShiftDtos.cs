using System.ComponentModel.DataAnnotations;
using Savvy.Domain.Entities;

namespace Savvy.Application.Shifts;

/// <summary>Shift as returned by the API.</summary>
public record ShiftResponse
{
    public int Id { get; init; }
    public int PracticeId { get; init; }
    public int? ClinicianId { get; init; }
    public DateOnly Date { get; init; }
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public decimal HourlyRate { get; init; }
    public string Role { get; init; } = null!;
    public string Location { get; init; } = null!;
    public string Status { get; init; } = null!;

    public static ShiftResponse From(Shift s) => new()
    {
        Id = s.Id,
        PracticeId = s.PracticeId,
        ClinicianId = s.ClinicianId,
        Date = s.Date,
        StartUtc = s.StartUtc,
        EndUtc = s.EndUtc,
        HourlyRate = s.HourlyRate,
        Role = s.Role,
        Location = s.Location,
        Status = s.Status.ToString()
    };
}

/// <summary>Create a shift under a practice. Basic shape is validated via data annotations;
/// cross-field and referential rules are enforced in the service.</summary>
public record CreateShiftRequest
{
    [Required] public DateOnly Date { get; init; }
    [Required] public DateTime StartUtc { get; init; }
    [Required] public DateTime EndUtc { get; init; }

    [Range(0.01, 100000)] public decimal HourlyRate { get; init; }

    [Required, StringLength(100)] public string Role { get; init; } = null!;
    [Required, StringLength(200)] public string Location { get; init; } = null!;

    /// <summary>Optional clinician assignment (must belong to the same practice).</summary>
    public int? ClinicianId { get; init; }
}

/// <summary>Update a shift's scheduling details / assignment. Status is system-driven
/// (set when a timesheet is submitted) and is not editable here.</summary>
public record UpdateShiftRequest
{
    [Required] public DateOnly Date { get; init; }
    [Required] public DateTime StartUtc { get; init; }
    [Required] public DateTime EndUtc { get; init; }

    [Range(0.01, 100000)] public decimal HourlyRate { get; init; }

    [Required, StringLength(100)] public string Role { get; init; } = null!;
    [Required, StringLength(200)] public string Location { get; init; } = null!;

    public int? ClinicianId { get; init; }
}
