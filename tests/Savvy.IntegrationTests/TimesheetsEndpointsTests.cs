using System.Net;
using System.Net.Http.Json;
using Savvy.Application.Timesheets;
using Savvy.Domain.Enums;
using Savvy.IntegrationTests.Infrastructure;
using Xunit;

namespace Savvy.IntegrationTests;

public class TimesheetsEndpointsTests : IntegrationTestBase
{
    private const int PracticeId = 1;

    // An Open shift assigned to a clinician (ready to be timesheeted).
    private (int ShiftId, int ClinicianId) ReadyShift() =>
        UseDb(db =>
        {
            var s = db.Shifts.First(x => x.ClinicianId != null && x.Status == ShiftStatus.Open);
            return (s.Id, s.ClinicianId!.Value);
        });

    private static SubmitTimesheetRequest Request(string reference, int breakMinutes = 30) => new()
    {
        WorkedStartUtc = new DateTime(2026, 7, 14, 8, 0, 0, DateTimeKind.Utc),
        WorkedEndUtc = new DateTime(2026, 7, 14, 16, 0, 0, DateTimeKind.Utc),
        UnpaidBreakMinutes = breakMinutes,
        Notes = "Seen 12 patients",
        BusinessReference = reference
    };

    [Fact]
    public async Task Submit_creates_timesheet_computes_hours_and_completes_shift()
    {
        var (shiftId, clinicianId) = ReadyShift();
        var client = Client("Clinician", uid: clinicianId, practiceId: PracticeId);

        var resp = await client.PostAsJsonAsync($"/api/shifts/{shiftId}/timesheets", Request("TS-001"));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var ts = await resp.Content.ReadFromJsonAsync<TimesheetResponse>();
        Assert.NotNull(ts);
        Assert.Equal(7.50m, ts!.Hours); // 8h - 30min break
        Assert.NotEqual(Guid.Empty, ts.PublicId);

        // Shift flipped to Completed.
        var status = UseDb(db => db.Shifts.First(s => s.Id == shiftId).Status);
        Assert.Equal(ShiftStatus.Completed, status);
    }

    [Fact]
    public async Task Duplicate_reference_same_payload_returns_original_without_duplicating()
    {
        var (shiftId, clinicianId) = ReadyShift();
        var client = Client("Clinician", uid: clinicianId, practiceId: PracticeId);

        var first = await client.PostAsJsonAsync($"/api/shifts/{shiftId}/timesheets", Request("TS-DUP"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstTs = await first.Content.ReadFromJsonAsync<TimesheetResponse>();

        // Replay the exact same submission.
        var second = await client.PostAsJsonAsync($"/api/shifts/{shiftId}/timesheets", Request("TS-DUP"));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode); // 200, not 201
        var secondTs = await second.Content.ReadFromJsonAsync<TimesheetResponse>();

        Assert.Equal(firstTs!.PublicId, secondTs!.PublicId);
        Assert.Equal(1, UseDb(db => db.Timesheets.Count(t => t.BusinessReference == "TS-DUP")));
    }

    [Fact]
    public async Task Same_reference_different_payload_is_409()
    {
        var (shiftId, clinicianId) = ReadyShift();
        var client = Client("Clinician", uid: clinicianId, practiceId: PracticeId);

        await client.PostAsJsonAsync($"/api/shifts/{shiftId}/timesheets", Request("TS-CONF", breakMinutes: 30));
        var conflict = await client.PostAsJsonAsync($"/api/shifts/{shiftId}/timesheets", Request("TS-CONF", breakMinutes: 45));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Submit_for_a_shift_not_owned_is_403()
    {
        var (shiftId, clinicianId) = ReadyShift();
        var notOwner = Client("Clinician", uid: clinicianId + 1000, practiceId: PracticeId);

        var resp = await notOwner.PostAsJsonAsync($"/api/shifts/{shiftId}/timesheets", Request("TS-OWN"));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Submit_to_already_completed_shift_is_409()
    {
        var (shiftId, clinicianId) = ReadyShift();
        var client = Client("Clinician", uid: clinicianId, practiceId: PracticeId);

        await client.PostAsJsonAsync($"/api/shifts/{shiftId}/timesheets", Request("TS-A"));
        var second = await client.PostAsJsonAsync($"/api/shifts/{shiftId}/timesheets", Request("TS-B"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Non_clinician_submit_is_403_by_role()
    {
        var (shiftId, _) = ReadyShift();
        var manager = Client("PracticeManager", uid: 2, practiceId: PracticeId);

        var resp = await manager.PostAsJsonAsync($"/api/shifts/{shiftId}/timesheets", Request("TS-ROLE"));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Timesheet_over_9_hours_raises_a_warning()
    {
        var (shiftId, clinicianId) = ReadyShift();
        var client = Client("Clinician", uid: clinicianId, practiceId: PracticeId);

        // 08:00–18:00 with no break = 10.00 hours (> 9h threshold).
        var req = new SubmitTimesheetRequest
        {
            WorkedStartUtc = new DateTime(2026, 7, 14, 8, 0, 0, DateTimeKind.Utc),
            WorkedEndUtc = new DateTime(2026, 7, 14, 18, 0, 0, DateTimeKind.Utc),
            UnpaidBreakMinutes = 0,
            BusinessReference = "TS-LONG"
        };

        (await client.PostAsJsonAsync($"/api/shifts/{shiftId}/timesheets", req)).EnsureSuccessStatusCode();

        var warning = Assert.Single(Factory.Notifications.Warnings);
        Assert.Equal(10.00m, warning.Hours);
        Assert.Equal(9m, warning.ThresholdHours);
    }

    [Fact]
    public async Task Timesheet_of_exactly_9_hours_does_not_warn()
    {
        var (shiftId, clinicianId) = ReadyShift();
        var client = Client("Clinician", uid: clinicianId, practiceId: PracticeId);

        // 08:00–17:00 with no break = 9.00 hours exactly (not > 9h).
        var req = new SubmitTimesheetRequest
        {
            WorkedStartUtc = new DateTime(2026, 7, 14, 8, 0, 0, DateTimeKind.Utc),
            WorkedEndUtc = new DateTime(2026, 7, 14, 17, 0, 0, DateTimeKind.Utc),
            UnpaidBreakMinutes = 0,
            BusinessReference = "TS-9H"
        };

        (await client.PostAsJsonAsync($"/api/shifts/{shiftId}/timesheets", req)).EnsureSuccessStatusCode();

        Assert.Empty(Factory.Notifications.Warnings);
        Assert.Single(Factory.Notifications.Submitted); // normal notification still fired
    }

    [Fact]
    public async Task Get_timesheet_respects_ownership_and_practice_scope()
    {
        var (shiftId, clinicianId) = ReadyShift();
        var owner = Client("Clinician", uid: clinicianId, practiceId: PracticeId);

        var created = await owner.PostAsJsonAsync($"/api/shifts/{shiftId}/timesheets", Request("TS-GET"));
        var ts = await created.Content.ReadFromJsonAsync<TimesheetResponse>();
        var url = $"/api/timesheets/{ts!.PublicId}";

        // Owning clinician
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(url)).StatusCode);
        // Manager of the owning practice
        Assert.Equal(HttpStatusCode.OK,
            (await Client("PracticeManager", uid: 2, practiceId: PracticeId).GetAsync(url)).StatusCode);
        // Manager of a different practice
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Client("PracticeManager", uid: 2, practiceId: 999).GetAsync(url)).StatusCode);
        // A different clinician
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Client("Clinician", uid: clinicianId + 1000, practiceId: PracticeId).GetAsync(url)).StatusCode);
    }
}
