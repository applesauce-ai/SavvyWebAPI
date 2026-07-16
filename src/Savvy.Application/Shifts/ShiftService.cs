using Microsoft.EntityFrameworkCore;
using Savvy.Application.Common;
using Savvy.Domain.Entities;
using Savvy.Domain.Enums;

namespace Savvy.Application.Shifts;

public class ShiftService : IShiftService
{
    private readonly ISavvyDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public ShiftService(ISavvyDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ShiftResponse>> ListForPracticeAsync(int practiceId, CancellationToken ct = default)
    {
        await EnsurePracticeExistsAsync(practiceId, ct);
        EnsurePracticeAccess(practiceId);

        var shifts = await _db.Shifts
            .AsNoTracking()
            .Where(s => s.PracticeId == practiceId)
            .OrderBy(s => s.StartUtc)
            .ToListAsync(ct);

        return shifts.Select(ShiftResponse.From).ToList();
    }

    public async Task<ShiftResponse> CreateAsync(int practiceId, CreateShiftRequest request, CancellationToken ct = default)
    {
        await EnsurePracticeExistsAsync(practiceId, ct);
        EnsurePracticeAccess(practiceId);
        EnsureValidSchedule(request.StartUtc, request.EndUtc);
        await EnsureClinicianValidAsync(request.ClinicianId, practiceId, ct);

        var shift = new Shift
        {
            PracticeId = practiceId,
            ClinicianId = request.ClinicianId,
            Date = request.Date,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            HourlyRate = request.HourlyRate,
            Role = request.Role,
            Location = request.Location,
            Status = ShiftStatus.Open
        };

        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync(ct);

        return ShiftResponse.From(shift);
    }

    public async Task<ShiftResponse> UpdateAsync(int shiftId, UpdateShiftRequest request, CancellationToken ct = default)
    {
        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId, ct)
            ?? throw NotFoundException.For("Shift", shiftId);

        // Scope to the shift's own practice.
        EnsurePracticeAccess(shift.PracticeId);

        if (shift.Status == ShiftStatus.Completed)
            throw new ValidationException("A completed shift cannot be modified.");

        EnsureValidSchedule(request.StartUtc, request.EndUtc);
        await EnsureClinicianValidAsync(request.ClinicianId, shift.PracticeId, ct);

        shift.Date = request.Date;
        shift.StartUtc = request.StartUtc;
        shift.EndUtc = request.EndUtc;
        shift.HourlyRate = request.HourlyRate;
        shift.Role = request.Role;
        shift.Location = request.Location;
        shift.ClinicianId = request.ClinicianId;

        await _db.SaveChangesAsync(ct);

        return ShiftResponse.From(shift);
    }

    // --- helpers ---

    private async Task EnsurePracticeExistsAsync(int practiceId, CancellationToken ct)
    {
        var exists = await _db.Practices.AnyAsync(p => p.Id == practiceId, ct);
        if (!exists) throw NotFoundException.For("Practice", practiceId);
    }

    /// <summary>Admin may access any practice; others only their own.</summary>
    private void EnsurePracticeAccess(int practiceId)
    {
        if (_currentUser.IsAdmin) return;
        if (_currentUser.PracticeId != practiceId) throw new ForbiddenException();
    }

    private static void EnsureValidSchedule(DateTime startUtc, DateTime endUtc)
    {
        if (endUtc <= startUtc)
            throw new ValidationException("EndUtc must be after StartUtc.");
    }

    private async Task EnsureClinicianValidAsync(int? clinicianId, int practiceId, CancellationToken ct)
    {
        if (clinicianId is null) return;

        var clinician = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == clinicianId, ct)
            ?? throw new ValidationException($"Clinician '{clinicianId}' was not found.");

        if (clinician.Role.Name != "Clinician")
            throw new ValidationException("Assigned user is not a Clinician.");

        if (clinician.PracticeId != practiceId)
            throw new ValidationException("Clinician belongs to a different practice.");
    }
}
