using Microsoft.EntityFrameworkCore;
using Savvy.Application.Common;
using Savvy.Application.Notifications;
using Savvy.Domain.Entities;
using Savvy.Domain.Enums;

namespace Savvy.Application.Timesheets;

public class TimesheetService : ITimesheetService
{
    /// <summary>Timesheets with more worked hours than this raise a Discord warning.
    /// A business rule kept here as a constant; could be moved to configuration if it needs tuning.</summary>
    public const decimal LongTimesheetThresholdHours = 9m;

    private readonly ISavvyDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly INotificationService _notifications;

    public TimesheetService(ISavvyDbContext db, ICurrentUserContext currentUser, INotificationService notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<TimesheetSubmissionResult> SubmitAsync(int shiftId, SubmitTimesheetRequest request, CancellationToken ct = default)
    {
        // 1. Idempotency: a repeated BusinessReference is a no-op that returns the original,
        //    unless the payload materially differs (then it's a conflict).
        var existing = await _db.Timesheets
            .FirstOrDefaultAsync(t => t.BusinessReference == request.BusinessReference, ct);

        if (existing is not null)
        {
            if (existing.ClinicianId != _currentUser.UserId)
                throw new ConflictException("BusinessReference is already in use.");

            if (IsSamePayload(existing, shiftId, request))
                return new TimesheetSubmissionResult(TimesheetResponse.From(existing), Created: false);

            throw new ConflictException(
                "This BusinessReference was already submitted with different details.");
        }

        // 2. New submission — validate the shift and ownership.
        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId, ct)
            ?? throw NotFoundException.For("Shift", shiftId);

        if (shift.ClinicianId != _currentUser.UserId)
            throw new ForbiddenException("You can only submit timesheets for your own shifts.");

        if (shift.Status != ShiftStatus.Open)
            throw new ConflictException("This shift has already been timesheeted.");

        ValidateWorkedPeriod(request);

        var timesheet = new Timesheet
        {
            ShiftId = shift.Id,
            ClinicianId = _currentUser.UserId,
            WorkedStartUtc = request.WorkedStartUtc,
            WorkedEndUtc = request.WorkedEndUtc,
            UnpaidBreakMinutes = request.UnpaidBreakMinutes,
            Notes = request.Notes,
            BusinessReference = request.BusinessReference,
            CreatedAtUtc = DateTime.UtcNow
        };

        // 3. Submitting completes the shift.
        shift.Status = ShiftStatus.Completed;
        _db.Timesheets.Add(timesheet);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Concurrent submit won the race (unique index on ShiftId / BusinessReference).
            // Reload and return the winner idempotently if it matches; else surface a conflict.
            var winner = await _db.Timesheets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.BusinessReference == request.BusinessReference, ct);

            if (winner is not null && IsSamePayload(winner, shiftId, request))
                return new TimesheetSubmissionResult(TimesheetResponse.From(winner), Created: false);

            throw new ConflictException("This shift has already been timesheeted.");
        }

        // Best-effort notifications for a genuinely new submission (not idempotent replays).
        var hours = Domain.Calculations.WorkHours.ComputeHours(
            timesheet.WorkedStartUtc, timesheet.WorkedEndUtc, timesheet.UnpaidBreakMinutes);

        await _notifications.TimesheetSubmittedAsync(
            new TimesheetSubmittedEvent(timesheet.PublicId, timesheet.ShiftId, timesheet.ClinicianId, hours, timesheet.BusinessReference),
            ct);

        if (hours > LongTimesheetThresholdHours)
        {
            await _notifications.TimesheetHoursWarningAsync(
                new TimesheetHoursWarningEvent(timesheet.PublicId, timesheet.ShiftId, timesheet.ClinicianId,
                    hours, LongTimesheetThresholdHours, timesheet.BusinessReference),
                ct);
        }

        return new TimesheetSubmissionResult(TimesheetResponse.From(timesheet), Created: true);
    }

    public async Task<TimesheetResponse> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default)
    {
        var timesheet = await _db.Timesheets
            .AsNoTracking()
            .Include(t => t.Shift)
            .FirstOrDefaultAsync(t => t.PublicId == publicId, ct)
            ?? throw NotFoundException.For("Timesheet", publicId);

        EnsureCanView(timesheet);

        return TimesheetResponse.From(timesheet);
    }

    // --- helpers ---

    private static bool IsSamePayload(Timesheet existing, int shiftId, SubmitTimesheetRequest request)
        => existing.ShiftId == shiftId
           && existing.WorkedStartUtc == request.WorkedStartUtc
           && existing.WorkedEndUtc == request.WorkedEndUtc
           && existing.UnpaidBreakMinutes == request.UnpaidBreakMinutes;

    private static void ValidateWorkedPeriod(SubmitTimesheetRequest request)
    {
        if (request.WorkedEndUtc <= request.WorkedStartUtc)
            throw new ValidationException("WorkedEndUtc must be after WorkedStartUtc.");

        var grossMinutes = (request.WorkedEndUtc - request.WorkedStartUtc).TotalMinutes;
        if (request.UnpaidBreakMinutes >= grossMinutes)
            throw new ValidationException("Unpaid break cannot be equal to or exceed the worked period.");
    }

    /// <summary>Admin: any. PracticeManager: own practice. Clinician: only their own timesheet.</summary>
    private void EnsureCanView(Timesheet timesheet)
    {
        if (_currentUser.IsAdmin) return;

        switch (_currentUser.Role)
        {
            case "PracticeManager" when timesheet.Shift.PracticeId == _currentUser.PracticeId:
            case "Clinician" when timesheet.ClinicianId == _currentUser.UserId:
                return;
            default:
                throw new ForbiddenException();
        }
    }
}
