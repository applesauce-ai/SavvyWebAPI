namespace Savvy.Application.Timesheets;

public interface ITimesheetService
{
    Task<TimesheetSubmissionResult> SubmitAsync(int shiftId, SubmitTimesheetRequest request, CancellationToken ct = default);
    Task<TimesheetResponse> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default);
}
