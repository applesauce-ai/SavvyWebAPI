using System.Net;
using System.Net.Http.Json;
using Savvy.Application.PaymentRuns;
using Savvy.Application.Timesheets;
using Savvy.Domain.Enums;
using Savvy.IntegrationTests.Infrastructure;
using Xunit;

namespace Savvy.IntegrationTests;

public class PaymentRunsEndpointsTests : IntegrationTestBase
{
    private const int PracticeId = 1;

    private (int ShiftId, int ClinicianId) ReadyShift() =>
        UseDb(db =>
        {
            var s = db.Shifts.First(x => x.ClinicianId != null && x.Status == ShiftStatus.Open);
            return (s.Id, s.ClinicianId!.Value);
        });

    /// <summary>Submit a 7.50-hour timesheet (08:00–16:00 less 30 min) at the seeded £25 rate.</summary>
    private async Task SubmitTimesheetAsync(int shiftId, int clinicianId)
    {
        var client = Client("Clinician", uid: clinicianId, practiceId: PracticeId);
        var req = new SubmitTimesheetRequest
        {
            WorkedStartUtc = new DateTime(2026, 7, 14, 8, 0, 0, DateTimeKind.Utc),
            WorkedEndUtc = new DateTime(2026, 7, 14, 16, 0, 0, DateTimeKind.Utc),
            UnpaidBreakMinutes = 30,
            BusinessReference = "TS-PR"
        };
        var resp = await client.PostAsJsonAsync($"/api/shifts/{shiftId}/timesheets", req);
        resp.EnsureSuccessStatusCode();
    }

    private static CreatePaymentRunRequest RunRequest(string reference, decimal feePct = 0.15m, decimal fixedFee = 5.00m) => new()
    {
        PeriodStartUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        PeriodEndUtc = new DateTime(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc),
        FeePercentage = feePct,
        FixedFeePerTimesheet = fixedFee,
        Currency = "GBP",
        BusinessReference = reference
    };

    [Fact]
    public async Task Create_computes_line_level_totals_with_away_from_zero_rounding()
    {
        var (shiftId, clinicianId) = ReadyShift();
        await SubmitTimesheetAsync(shiftId, clinicianId);

        var manager = Client("PracticeManager", uid: 2, practiceId: PracticeId);
        var resp = await manager.PostAsJsonAsync($"/api/practices/{PracticeId}/payment-runs", RunRequest("PR-001"));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var run = await resp.Content.ReadFromJsonAsync<PaymentRunResponse>();
        Assert.NotNull(run);

        // 7.50h × £25 = 187.50 gross; fee = 187.50×0.15 + 5.00 = 33.125 -> 33.13 (away from zero);
        // net = 187.50 − 33.13 = 154.37.
        Assert.Single(run!.LineItems);
        Assert.Equal(187.50m, run.GrossTotal);
        Assert.Equal(33.13m, run.FeeTotal);
        Assert.Equal(154.37m, run.NetTotal);

        var line = run.LineItems[0];
        Assert.Equal(7.50m, line.Hours);
        Assert.Equal(25.00m, line.Rate);
        Assert.Equal(187.50m, line.Gross);
        Assert.Equal(33.13m, line.Fee);
        Assert.Equal(154.37m, line.Net);
    }

    [Fact]
    public async Task Duplicate_reference_same_request_is_idempotent()
    {
        var (shiftId, clinicianId) = ReadyShift();
        await SubmitTimesheetAsync(shiftId, clinicianId);
        var manager = Client("PracticeManager", uid: 2, practiceId: PracticeId);

        var first = await manager.PostAsJsonAsync($"/api/practices/{PracticeId}/payment-runs", RunRequest("PR-DUP"));
        var firstRun = await first.Content.ReadFromJsonAsync<PaymentRunResponse>();

        var second = await manager.PostAsJsonAsync($"/api/practices/{PracticeId}/payment-runs", RunRequest("PR-DUP"));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode); // 200, not 201
        var secondRun = await second.Content.ReadFromJsonAsync<PaymentRunResponse>();

        Assert.Equal(firstRun!.PublicId, secondRun!.PublicId);
        Assert.Equal(1, UseDb(db => db.PaymentRuns.Count(r => r.BusinessReference == "PR-DUP")));
    }

    [Fact]
    public async Task Same_reference_different_params_is_409()
    {
        var manager = Client("PracticeManager", uid: 2, practiceId: PracticeId);

        await manager.PostAsJsonAsync($"/api/practices/{PracticeId}/payment-runs", RunRequest("PR-CONF", feePct: 0.15m));
        var conflict = await manager.PostAsJsonAsync($"/api/practices/{PracticeId}/payment-runs", RunRequest("PR-CONF", feePct: 0.20m));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Manager_cannot_run_for_another_practice()
    {
        var otherPracticeManager = Client("PracticeManager", uid: 2, practiceId: 999);
        var resp = await otherPracticeManager.PostAsJsonAsync($"/api/practices/{PracticeId}/payment-runs", RunRequest("PR-X"));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Empty_period_creates_run_with_zero_totals()
    {
        var manager = Client("PracticeManager", uid: 2, practiceId: PracticeId);
        var emptyReq = RunRequest("PR-EMPTY") with
        {
            PeriodStartUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEndUtc = new DateTime(2020, 12, 31, 23, 59, 59, DateTimeKind.Utc)
        };

        var resp = await manager.PostAsJsonAsync($"/api/practices/{PracticeId}/payment-runs", emptyReq);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var run = await resp.Content.ReadFromJsonAsync<PaymentRunResponse>();
        Assert.Empty(run!.LineItems);
        Assert.Equal(0m, run.GrossTotal);
        Assert.Equal(0m, run.NetTotal);
    }

    [Fact]
    public async Task Get_payment_run_respects_practice_scope()
    {
        var (shiftId, clinicianId) = ReadyShift();
        await SubmitTimesheetAsync(shiftId, clinicianId);
        var manager = Client("PracticeManager", uid: 2, practiceId: PracticeId);

        var created = await manager.PostAsJsonAsync($"/api/practices/{PracticeId}/payment-runs", RunRequest("PR-GET"));
        var run = await created.Content.ReadFromJsonAsync<PaymentRunResponse>();
        var url = $"/api/payment-runs/{run!.PublicId}";

        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client("Admin", uid: 1).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Client("PracticeManager", uid: 2, practiceId: 999).GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Clinician_cannot_create_payment_run()
    {
        var clinician = Client("Clinician", uid: 3, practiceId: PracticeId);
        var resp = await clinician.PostAsJsonAsync($"/api/practices/{PracticeId}/payment-runs", RunRequest("PR-ROLE"));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
