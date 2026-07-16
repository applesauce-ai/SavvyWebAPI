using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Savvy.Application.Timesheets;

namespace Savvy.Api.Controllers;

[ApiController]
public class TimesheetsController : ControllerBase
{
    private readonly ITimesheetService _timesheets;

    public TimesheetsController(ITimesheetService timesheets) => _timesheets = timesheets;

    /// <summary>Submit a timesheet for a shift. Idempotent on BusinessReference:
    /// a repeat returns the original (200); a genuinely new one is created (201).</summary>
    [HttpPost("api/shifts/{shiftId:int}/timesheets")]
    [Authorize(Roles = "Clinician")]
    public async Task<ActionResult<TimesheetResponse>> Submit(int shiftId, SubmitTimesheetRequest request, CancellationToken ct)
    {
        var result = await _timesheets.SubmitAsync(shiftId, request, ct);

        return result.Created
            ? CreatedAtAction(nameof(GetByPublicId), new { publicId = result.Timesheet.PublicId }, result.Timesheet)
            : Ok(result.Timesheet);
    }

    /// <summary>Get a timesheet by its public id. Visible to Admin, the owning practice's
    /// manager, and the owning clinician.</summary>
    [HttpGet("api/timesheets/{publicId:guid}")]
    [Authorize(Roles = "Admin,PracticeManager,Clinician")]
    public async Task<ActionResult<TimesheetResponse>> GetByPublicId(Guid publicId, CancellationToken ct)
        => Ok(await _timesheets.GetByPublicIdAsync(publicId, ct));
}
