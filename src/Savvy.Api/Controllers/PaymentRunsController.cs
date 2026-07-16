using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Savvy.Application.PaymentRuns;

namespace Savvy.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,PracticeManager")]
public class PaymentRunsController : ControllerBase
{
    private readonly IPaymentRunService _paymentRuns;

    public PaymentRunsController(IPaymentRunService paymentRuns) => _paymentRuns = paymentRuns;

    /// <summary>Create a payment run for a practice over a UTC period. Idempotent on
    /// BusinessReference: a repeat returns the original (200); a new one is created (201).</summary>
    [HttpPost("api/practices/{practiceId:int}/payment-runs")]
    public async Task<ActionResult<PaymentRunResponse>> Create(int practiceId, CreatePaymentRunRequest request, CancellationToken ct)
    {
        var result = await _paymentRuns.CreateAsync(practiceId, request, ct);

        return result.Created
            ? CreatedAtAction(nameof(GetByPublicId), new { publicId = result.Run.PublicId }, result.Run)
            : Ok(result.Run);
    }

    /// <summary>Get a payment run (summary + line items) by its public id.</summary>
    [HttpGet("api/payment-runs/{publicId:guid}")]
    public async Task<ActionResult<PaymentRunResponse>> GetByPublicId(Guid publicId, CancellationToken ct)
        => Ok(await _paymentRuns.GetByPublicIdAsync(publicId, ct));
}
