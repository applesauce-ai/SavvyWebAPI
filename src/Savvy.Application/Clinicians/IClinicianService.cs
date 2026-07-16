namespace Savvy.Application.Clinicians;

public interface IClinicianService
{
    Task<ClinicianResponse> CreateAsync(int practiceId, CreateClinicianRequest request, CancellationToken ct = default);
}
