using Microsoft.Extensions.DependencyInjection;
using Savvy.Infrastructure.Persistence;

namespace Savvy.IntegrationTests.Infrastructure;

/// <summary>
/// Base for endpoint tests. xUnit constructs a new test-class instance per test method, so each
/// test gets its own factory and a fresh, isolated SQLite database — no cross-test coupling even
/// when a test mutates state (e.g. completing a shift).
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected readonly SavvyApiFactory Factory = new();

    /// <summary>Create a client acting as the given role (headers consumed by TestAuthHandler).</summary>
    protected HttpClient Client(string? role = null, int? uid = null, int? practiceId = null, Guid? sub = null)
    {
        var client = Factory.CreateClient();
        if (role is not null) client.DefaultRequestHeaders.Add("X-Test-Role", role);
        if (uid is not null) client.DefaultRequestHeaders.Add("X-Test-Uid", uid.ToString());
        if (practiceId is not null) client.DefaultRequestHeaders.Add("X-Test-PracticeId", practiceId.ToString());
        if (sub is not null) client.DefaultRequestHeaders.Add("X-Test-Sub", sub.ToString());
        return client;
    }

    /// <summary>Run a read against the seeded database (for arranging/asserting test state).</summary>
    protected T UseDb<T>(Func<SavvyDbContext, T> query)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SavvyDbContext>();
        return query(db);
    }

    public void Dispose() => Factory.Dispose();
}
