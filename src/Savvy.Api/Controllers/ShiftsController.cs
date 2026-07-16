using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Savvy.Application.Shifts;

namespace Savvy.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,PracticeManager")]
public class ShiftsController : ControllerBase
{
    private readonly IShiftService _shifts;

    public ShiftsController(IShiftService shifts) => _shifts = shifts;

    /// <summary>List shifts for a practice (scoped: PracticeManager sees only their own).</summary>
    [HttpGet("api/practices/{practiceId:int}/shifts")]
    public async Task<ActionResult<IReadOnlyList<ShiftResponse>>> List(int practiceId, CancellationToken ct)
        => Ok(await _shifts.ListForPracticeAsync(practiceId, ct));

    /// <summary>Create a shift under a practice.</summary>
    [HttpPost("api/practices/{practiceId:int}/shifts")]
    public async Task<ActionResult<ShiftResponse>> Create(int practiceId, CreateShiftRequest request, CancellationToken ct)
    {
        var created = await _shifts.CreateAsync(practiceId, request, ct);
        return CreatedAtAction(nameof(List), new { practiceId }, created);
    }

    /// <summary>Update a shift's scheduling details / assignment.</summary>
    [HttpPut("api/shifts/{id:int}")]
    public async Task<ActionResult<ShiftResponse>> Update(int id, UpdateShiftRequest request, CancellationToken ct)
        => Ok(await _shifts.UpdateAsync(id, request, ct));
}
