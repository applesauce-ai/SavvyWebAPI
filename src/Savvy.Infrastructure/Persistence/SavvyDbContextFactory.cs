using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Savvy.Infrastructure.Persistence;

/// <summary>
/// Enables design-time tooling (<c>dotnet ef migrations add</c> / <c>database update</c>)
/// to construct the context without booting the full API host.
/// The connection string is read from the SAVVY_CONNECTION environment variable,
/// falling back to a local SQL Server LocalDB instance for developer machines.
/// </summary>
public class SavvyDbContextFactory : IDesignTimeDbContextFactory<SavvyDbContext>
{
    public SavvyDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("SAVVY_CONNECTION")
            ?? "Server=localhost;Database=SavvyDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True";

        var options = new DbContextOptionsBuilder<SavvyDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(SavvyDbContext).Assembly.FullName))
            .Options;

        return new SavvyDbContext(options);
    }
}
