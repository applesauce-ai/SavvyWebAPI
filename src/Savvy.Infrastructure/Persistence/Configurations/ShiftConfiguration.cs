using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Savvy.Domain.Entities;

namespace Savvy.Infrastructure.Persistence.Configurations;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Date).HasColumnType("date");
        builder.Property(s => s.StartUtc).IsRequired();
        builder.Property(s => s.EndUtc).IsRequired();

        builder.Property(s => s.HourlyRate).HasPrecision(18, 2);

        builder.Property(s => s.Role).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Location).IsRequired().HasMaxLength(200);

        builder.Property(s => s.Status)
            .HasConversion<int>()   // persist enum as int
            .IsRequired();

        // Practice / Clinician FKs configured from the principal side.
        // Timesheet (1:0..1) configured in TimesheetConfiguration via the unique ShiftId.

        builder.HasIndex(s => s.PracticeId);
        builder.HasIndex(s => s.ClinicianId);
    }
}
