using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Savvy.Application.Auth;
using Savvy.Application.PaymentRuns;
using Savvy.Application.Shifts;
using Savvy.Application.Timesheets;
using Savvy.Domain.Enums;
using Savvy.Infrastructure.Persistence;
using Savvy.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Savvy.IntegrationTests;

/// <summary>
/// Full flow through the REAL JWT pipeline (no test auth handler): each role logs in via
/// /api/auth/login, then drives create-shift → submit-timesheet → run-payment and the totals
/// are checked against a hand calculation.
/// </summary>
public class HappyPathE2ETests : IDisposable
{
    private readonly SavvyApiFactory _factory = new(useTestAuth: false);

    public void Dispose() => _factory.Dispose();

    private async Task<string> LoginAsync(string email, string password)
    {
        var resp = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.AccessToken;
    }

    private HttpClient Authed(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Bad_credentials_are_rejected()
    {
        var resp = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "admin@savvy.test", Password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_create_shift_submit_timesheet_run_payment()
    {
        // Seeded ids: practice 1; clinician is user id 3.
        const int practiceId = 1;
        var managerToken = await LoginAsync(SavvySeeder.ManagerEmail, SavvySeeder.ManagerPassword);
        var clinicianToken = await LoginAsync(SavvySeeder.ClinicianEmail, SavvySeeder.ClinicianPassword);

        var clinicianId = GetClinicianId();

        // 1. Manager creates a shift assigned to the clinician.
        var manager = Authed(managerToken);
        var createShift = new CreateShiftRequest
        {
            Date = new DateOnly(2026, 7, 20),
            StartUtc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2026, 7, 20, 16, 0, 0, DateTimeKind.Utc),
            HourlyRate = 25.00m,
            Role = "Nurse",
            Location = "Main Ward",
            ClinicianId = clinicianId
        };
        var shiftResp = await manager.PostAsJsonAsync($"/api/practices/{practiceId}/shifts", createShift);
        Assert.Equal(HttpStatusCode.Created, shiftResp.StatusCode);
        var shift = await shiftResp.Content.ReadFromJsonAsync<ShiftResponse>();

        // 2. Clinician submits a timesheet (7.5h).
        var clinician = Authed(clinicianToken);
        var submit = new SubmitTimesheetRequest
        {
            WorkedStartUtc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc),
            WorkedEndUtc = new DateTime(2026, 7, 20, 16, 0, 0, DateTimeKind.Utc),
            UnpaidBreakMinutes = 30,
            BusinessReference = "E2E-TS-1"
        };
        var tsResp = await clinician.PostAsJsonAsync($"/api/shifts/{shift!.Id}/timesheets", submit);
        Assert.Equal(HttpStatusCode.Created, tsResp.StatusCode);
        var ts = await tsResp.Content.ReadFromJsonAsync<TimesheetResponse>();
        Assert.Equal(7.50m, ts!.Hours);

        // 3. Manager runs payment for the period.
        var runReq = new CreatePaymentRunRequest
        {
            PeriodStartUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEndUtc = new DateTime(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc),
            FeePercentage = 0.15m,
            FixedFeePerTimesheet = 5.00m,
            Currency = "GBP",
            BusinessReference = "E2E-PR-1"
        };
        var runResp = await manager.PostAsJsonAsync($"/api/practices/{practiceId}/payment-runs", runReq);
        Assert.Equal(HttpStatusCode.Created, runResp.StatusCode);
        var run = await runResp.Content.ReadFromJsonAsync<PaymentRunResponse>();

        // Hand calculation: 7.5×25 = 187.50; fee 187.50×0.15 + 5 = 33.13; net 154.37.
        Assert.Single(run!.LineItems);
        Assert.Equal(187.50m, run.GrossTotal);
        Assert.Equal(33.13m, run.FeeTotal);
        Assert.Equal(154.37m, run.NetTotal);

        // 4. Shift is now Completed.
        var status = GetShiftStatus(shift.Id);
        Assert.Equal(ShiftStatus.Completed, status);
    }

    private int GetClinicianId()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SavvyDbContext>();
        return db.Users.First(u => u.Email == SavvySeeder.ClinicianEmail).Id;
    }

    private ShiftStatus GetShiftStatus(int shiftId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SavvyDbContext>();
        return db.Shifts.First(s => s.Id == shiftId).Status;
    }
}
