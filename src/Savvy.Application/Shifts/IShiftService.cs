namespace Savvy.Application.Shifts;

public interface IShiftService
{
    Task<IReadOnlyList<ShiftResponse>> ListForPracticeAsync(int practiceId, CancellationToken ct = default);
    Task<ShiftResponse> CreateAsync(int practiceId, CreateShiftRequest request, CancellationToken ct = default);
    Task<ShiftResponse> UpdateAsync(int shiftId, UpdateShiftRequest request, CancellationToken ct = default);
}
