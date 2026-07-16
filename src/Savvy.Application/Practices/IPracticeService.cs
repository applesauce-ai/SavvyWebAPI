namespace Savvy.Application.Practices;

public interface IPracticeService
{
    Task<PracticeResponse> CreateAsync(CreatePracticeRequest request, CancellationToken ct = default);
    Task<PracticeResponse> GetByIdAsync(int id, CancellationToken ct = default);
}
