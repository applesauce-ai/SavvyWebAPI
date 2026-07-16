namespace Savvy.Application.PaymentRuns;

public interface IPaymentRunService
{
    Task<PaymentRunResult> CreateAsync(int practiceId, CreatePaymentRunRequest request, CancellationToken ct = default);
    Task<PaymentRunResponse> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default);
}
