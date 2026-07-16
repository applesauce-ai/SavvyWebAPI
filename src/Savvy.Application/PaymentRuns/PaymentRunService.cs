using Microsoft.EntityFrameworkCore;
using Savvy.Application.Common;
using Savvy.Application.Notifications;
using Savvy.Domain.Calculations;
using Savvy.Domain.Entities;

namespace Savvy.Application.PaymentRuns;

public class PaymentRunService : IPaymentRunService
{
    private const string DefaultCurrency = "GBP";

    private readonly ISavvyDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly INotificationService _notifications;

    public PaymentRunService(ISavvyDbContext db, ICurrentUserContext currentUser, INotificationService notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<PaymentRunResult> CreateAsync(int practiceId, CreatePaymentRunRequest request, CancellationToken ct = default)
    {
        await EnsurePracticeExistsAsync(practiceId, ct);
        EnsurePracticeAccess(practiceId);

        if (request.PeriodEndUtc <= request.PeriodStartUtc)
            throw new ValidationException("PeriodEndUtc must be after PeriodStartUtc.");

        // Idempotency on BusinessReference: same request returns the original run; a reference
        // reused with materially different parameters is a conflict.
        var existing = await _db.PaymentRuns
            .Include(r => r.LineItems)
            .FirstOrDefaultAsync(r => r.BusinessReference == request.BusinessReference, ct);

        if (existing is not null)
        {
            if (IsSameRequest(existing, practiceId, request))
                return new PaymentRunResult(PaymentRunResponse.From(existing), Created: false);

            throw new ConflictException(
                "This BusinessReference was already used for a different payment run.");
        }

        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? DefaultCurrency
            : request.Currency.ToUpperInvariant();

        var run = BuildRun(practiceId, request, currency);

        // Pull the practice's timesheets whose worked period falls in [start, end] (inclusive).
        var timesheets = await _db.Timesheets
            .Include(t => t.Shift)
            .Where(t => t.Shift.PracticeId == practiceId
                        && t.WorkedStartUtc >= request.PeriodStartUtc
                        && t.WorkedStartUtc <= request.PeriodEndUtc)
            .OrderBy(t => t.Id)
            .ToListAsync(ct);

        foreach (var t in timesheets)
        {
            var hours = WorkHours.ComputeHours(t.WorkedStartUtc, t.WorkedEndUtc, t.UnpaidBreakMinutes);
            var rate = t.Shift.HourlyRate;
            var amounts = FeeCalculation.ComputeLine(hours, rate, request.FeePercentage, request.FixedFeePerTimesheet);

            run.LineItems.Add(new PaymentRunLineItem
            {
                TimesheetId = t.Id,
                ClinicianId = t.ClinicianId,
                Hours = hours,
                Rate = rate,
                Gross = amounts.Gross,
                Fee = amounts.Fee,
                Net = amounts.Net
            });
        }

        // Totals are the sum of the rounded line amounts.
        run.GrossTotal = run.LineItems.Sum(li => li.Gross);
        run.FeeTotal = run.LineItems.Sum(li => li.Fee);
        run.NetTotal = run.LineItems.Sum(li => li.Net);

        _db.PaymentRuns.Add(run);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Concurrent run with the same reference won the race.
            var winner = await _db.PaymentRuns
                .AsNoTracking()
                .Include(r => r.LineItems)
                .FirstOrDefaultAsync(r => r.BusinessReference == request.BusinessReference, ct);

            if (winner is not null && IsSameRequest(winner, practiceId, request))
                return new PaymentRunResult(PaymentRunResponse.From(winner), Created: false);

            throw new ConflictException(
                "This BusinessReference was already used for a different payment run.");
        }

        // Best-effort notification for a genuinely new run (not idempotent replays).
        await _notifications.PaymentRunCreatedAsync(
            new PaymentRunCreatedEvent(run.PublicId, run.PracticeId, run.PeriodStartUtc, run.PeriodEndUtc,
                run.Currency, run.GrossTotal, run.FeeTotal, run.NetTotal, run.LineItems.Count, run.BusinessReference),
            ct);

        return new PaymentRunResult(PaymentRunResponse.From(run), Created: true);
    }

    public async Task<PaymentRunResponse> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default)
    {
        var run = await _db.PaymentRuns
            .AsNoTracking()
            .Include(r => r.LineItems)
            .FirstOrDefaultAsync(r => r.PublicId == publicId, ct)
            ?? throw NotFoundException.For("PaymentRun", publicId);

        EnsurePracticeAccess(run.PracticeId);

        return PaymentRunResponse.From(run);
    }

    // --- helpers ---

    private static PaymentRun BuildRun(int practiceId, CreatePaymentRunRequest request, string currency) => new()
    {
        PracticeId = practiceId,
        PeriodStartUtc = request.PeriodStartUtc,
        PeriodEndUtc = request.PeriodEndUtc,
        FeePercentage = request.FeePercentage,
        FixedFeePerTimesheet = request.FixedFeePerTimesheet,
        BusinessReference = request.BusinessReference,
        Currency = currency,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static bool IsSameRequest(PaymentRun existing, int practiceId, CreatePaymentRunRequest request)
        => existing.PracticeId == practiceId
           && existing.PeriodStartUtc == request.PeriodStartUtc
           && existing.PeriodEndUtc == request.PeriodEndUtc
           && existing.FeePercentage == request.FeePercentage
           && existing.FixedFeePerTimesheet == request.FixedFeePerTimesheet;

    private async Task EnsurePracticeExistsAsync(int practiceId, CancellationToken ct)
    {
        if (!await _db.Practices.AnyAsync(p => p.Id == practiceId, ct))
            throw NotFoundException.For("Practice", practiceId);
    }

    private void EnsurePracticeAccess(int practiceId)
    {
        if (_currentUser.IsAdmin) return;
        if (_currentUser.PracticeId != practiceId) throw new ForbiddenException();
    }
}
