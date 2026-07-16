using Microsoft.EntityFrameworkCore;
using Savvy.Application.Common;
using Savvy.Domain.Entities;

namespace Savvy.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for the Savvy backend.
/// </summary>
public class SavvyDbContext : DbContext, ISavvyDbContext
{
    public SavvyDbContext(DbContextOptions<SavvyDbContext> options)
        : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Practice> Practices => Set<Practice>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Timesheet> Timesheets => Set<Timesheet>();
    public DbSet<PaymentRun> PaymentRuns => Set<PaymentRun>();
    public DbSet<PaymentRunLineItem> PaymentRunLineItems => Set<PaymentRunLineItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> classes in this assembly.
        // (Entity configurations are added in Section 2.)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SavvyDbContext).Assembly);
    }
}
