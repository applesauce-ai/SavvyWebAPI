using Microsoft.EntityFrameworkCore;
using Savvy.Domain.Entities;

namespace Savvy.Application.Common;

/// <summary>
/// Abstraction over the persistence context so application use-cases can be written and
/// unit-tested without depending on the EF Core provider. Implemented by SavvyDbContext.
/// </summary>
public interface ISavvyDbContext
{
    DbSet<Role> Roles { get; }
    DbSet<Practice> Practices { get; }
    DbSet<User> Users { get; }
    DbSet<Shift> Shifts { get; }
    DbSet<Timesheet> Timesheets { get; }
    DbSet<PaymentRun> PaymentRuns { get; }
    DbSet<PaymentRunLineItem> PaymentRunLineItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
