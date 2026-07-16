using System.Net;
using System.Net.Http.Json;
using Savvy.Application.Auth;
using Savvy.Application.Clinicians;
using Savvy.Application.Practices;
using Savvy.IntegrationTests.Infrastructure;
using Xunit;

namespace Savvy.IntegrationTests;

public class PracticesAndCliniciansEndpointsTests : IntegrationTestBase
{
    private const int SeededPracticeId = 1;

    [Fact]
    public async Task Admin_can_create_a_practice()
    {
        var admin = Client("Admin", uid: 1);

        var resp = await admin.PostAsJsonAsync("/api/practices", new CreatePracticeRequest { Name = "Riverside Clinic" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var created = await resp.Content.ReadFromJsonAsync<PracticeResponse>();
        Assert.Equal("Riverside Clinic", created!.Name);
        Assert.True(created.Id > 0);

        // Retrievable by id.
        var fetched = await admin.GetFromJsonAsync<PracticeResponse>($"/api/practices/{created.Id}");
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task Non_admin_cannot_create_a_practice()
    {
        var manager = Client("PracticeManager", uid: 2, practiceId: SeededPracticeId);
        var resp = await manager.PostAsJsonAsync("/api/practices", new CreatePracticeRequest { Name = "Nope Clinic" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Duplicate_practice_name_is_409()
    {
        var admin = Client("Admin", uid: 1);
        var resp = await admin.PostAsJsonAsync("/api/practices",
            new CreatePracticeRequest { Name = "Savvy Medical Practice" }); // already seeded
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_a_clinician_who_can_then_log_in()
    {
        var admin = Client("Admin", uid: 1);

        // Create a fresh practice, then a clinician within it.
        var practice = await (await admin.PostAsJsonAsync("/api/practices",
            new CreatePracticeRequest { Name = "Elm Street Practice" })).Content.ReadFromJsonAsync<PracticeResponse>();

        var request = new CreateClinicianRequest { Email = "new.clinician@savvy.test", Password = "Clinician#99999" };
        var resp = await admin.PostAsJsonAsync($"/api/practices/{practice!.Id}/clinicians", request);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var clinician = await resp.Content.ReadFromJsonAsync<ClinicianResponse>();
        Assert.Equal("Clinician", clinician!.Role);
        Assert.Equal(practice.Id, clinician.PracticeId);
        Assert.NotEqual(Guid.Empty, clinician.PublicId);

        // The new clinician can authenticate with the password we set (proves hashing + role wiring).
        var login = await Client().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "new.clinician@savvy.test", Password = "Clinician#99999" });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.Equal("Clinician", body!.Role);
    }

    [Fact]
    public async Task Creating_a_clinician_with_a_duplicate_email_is_409()
    {
        var admin = Client("Admin", uid: 1);
        var resp = await admin.PostAsJsonAsync($"/api/practices/{SeededPracticeId}/clinicians",
            new CreateClinicianRequest { Email = "clinician@savvy.test", Password = "Whatever#12345" }); // seeded email
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Creating_a_clinician_for_a_missing_practice_is_404()
    {
        var admin = Client("Admin", uid: 1);
        var resp = await admin.PostAsJsonAsync("/api/practices/9999/clinicians",
            new CreateClinicianRequest { Email = "ghost@savvy.test", Password = "Whatever#12345" });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_create_a_clinician()
    {
        var manager = Client("PracticeManager", uid: 2, practiceId: SeededPracticeId);
        var resp = await manager.PostAsJsonAsync($"/api/practices/{SeededPracticeId}/clinicians",
            new CreateClinicianRequest { Email = "blocked@savvy.test", Password = "Whatever#12345" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
