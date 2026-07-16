using System.Net;
using System.Net.Http.Json;
using Savvy.Application.Shifts;
using Savvy.IntegrationTests.Infrastructure;
using Xunit;

namespace Savvy.IntegrationTests;

public class ShiftsEndpointsTests : IntegrationTestBase
{
    // Seeded practice id is 1 (first practice inserted).
    private const int PracticeId = 1;

    [Fact]
    public async Task Unauthenticated_request_is_401()
    {
        var resp = await Client().GetAsync($"/api/practices/{PracticeId}/shifts");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Clinician_is_forbidden_by_role()
    {
        var resp = await Client("Clinician", uid: 3, practiceId: PracticeId)
            .GetAsync($"/api/practices/{PracticeId}/shifts");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Manager_can_list_own_practice_shifts()
    {
        var shifts = await Client("PracticeManager", uid: 2, practiceId: PracticeId)
            .GetFromJsonAsync<List<ShiftResponse>>($"/api/practices/{PracticeId}/shifts");
        Assert.NotNull(shifts);
        Assert.Equal(3, shifts!.Count); // seeded shifts
    }
    [Fact]
    public async Task CheckRoles()
    {
        var roles = await Client("Roles",)
            .GetFromJsonAsync<List<string>>($"/api/roles");
        Assert.NotNull(roles);
        Assert.Equal(3, roles!.Count); // total roles
    }

    [Fact]
    public async Task Manager_cannot_list_other_practice_shifts()
    {
        var resp = await Client("PracticeManager", uid: 2, practiceId: 999)
            .GetAsync($"/api/practices/{PracticeId}/shifts");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_any_practice_shifts()
    {
        var resp = await Client("Admin", uid: 1)
            .GetAsync($"/api/practices/{PracticeId}/shifts");
        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Manager_can_create_shift()
    {
        var request = new CreateShiftRequest
        {
            Date = new DateOnly(2026, 8, 1),
            StartUtc = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2026, 8, 1, 17, 0, 0, DateTimeKind.Utc),
            HourlyRate = 28.50m,
            Role = "Nurse",
            Location = "Ward B"
        };

        var resp = await Client("PracticeManager", uid: 2, practiceId: PracticeId)
            .PostAsJsonAsync($"/api/practices/{PracticeId}/shifts", request);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var created = await resp.Content.ReadFromJsonAsync<ShiftResponse>();
        Assert.NotNull(created);
        Assert.Equal("Open", created!.Status);
        Assert.Equal(28.50m, created.HourlyRate);
    }

    [Fact]
    public async Task Create_with_end_before_start_is_400()
    {
        var request = new CreateShiftRequest
        {
            Date = new DateOnly(2026, 8, 2),
            StartUtc = new DateTime(2026, 8, 2, 17, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc), // before start
            HourlyRate = 20m,
            Role = "Nurse",
            Location = "Ward B"
        };

        var resp = await Client("PracticeManager", uid: 2, practiceId: PracticeId)
            .PostAsJsonAsync($"/api/practices/{PracticeId}/shifts", request);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
