using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Savvy.Application.Clinicians;

namespace Savvy.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class CliniciansController : ControllerBase
{
    private readonly IClinicianService _clinicians;

    public CliniciansController(IClinicianService clinicians) => _clinicians = clinicians;

    /// <summary>Create a new Clinician user under a practice (Admin only).</summary>
    [HttpPost("api/practices/{practiceId:int}/clinicians")]
    public async Task<ActionResult<ClinicianResponse>> Create(int practiceId, CreateClinicianRequest request, CancellationToken ct)
    {
        var created = await _clinicians.CreateAsync(practiceId, request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }
}
