using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Savvy.Domain.Entities;

namespace Savvy.Infrastructure.Persistence.Configurations;

public class TimesheetConfiguration : IEntityTypeConfiguration<Timesheet>
{
    public void Configure(EntityTypeBuilder<Timesheet> builder)
    {
        builder.ToTable("Timesheets");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.PublicId).IsRequired();
        builder.HasIndex(t => t.PublicId).IsUnique();

        // Exactly one timesheet per shift, enforced at the DB level.
        builder.HasOne(t => t.Shift)
            .WithOne(s => s.Timesheet!)
            .HasForeignKey<Timesheet>(t => t.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => t.ShiftId).IsUnique();

        builder.HasOne(t => t.Clinician)
            .WithMany()
            .HasForeignKey(t => t.ClinicianId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(t => t.UnpaidBreakMinutes).IsRequired();
        builder.Property(t => t.Notes).HasMaxLength(1000);

        builder.Property(t => t.BusinessReference).IsRequired().HasMaxLength(100);
        builder.HasIndex(t => t.BusinessReference).IsUnique();

        builder.Property(t => t.CreatedAtUtc).IsRequired();
    }
}
