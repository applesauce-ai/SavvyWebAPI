using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Savvy.Application.Practices;

namespace Savvy.Api.Controllers;

[ApiController]
[Route("api/practices")]
[Authorize(Roles = "Admin")]
public class PracticesController : ControllerBase
{
    private readonly IPracticeService _practices;

    public PracticesController(IPracticeService practices) => _practices = practices;

    /// <summary>Create a new practice (Admin only).</summary>
    [HttpPost]
    public async Task<ActionResult<PracticeResponse>> Create(CreatePracticeRequest request, CancellationToken ct)
    {
        var created = await _practices.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Get a practice by id (Admin only).</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PracticeResponse>> GetById(int id, CancellationToken ct)
        => Ok(await _practices.GetByIdAsync(id, ct));
    oops;
}
