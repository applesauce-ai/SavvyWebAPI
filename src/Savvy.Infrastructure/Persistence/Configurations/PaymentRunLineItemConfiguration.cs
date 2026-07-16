using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Savvy.Domain.Entities;

namespace Savvy.Infrastructure.Persistence.Configurations;

public class PaymentRunLineItemConfiguration : IEntityTypeConfiguration<PaymentRunLineItem>
{
    public void Configure(EntityTypeBuilder<PaymentRunLineItem> builder)
    {
        builder.ToTable("PaymentRunLineItems");
        builder.HasKey(li => li.Id);

        builder.Property(li => li.Hours).HasPrecision(9, 2);
        builder.Property(li => li.Rate).HasPrecision(18, 2);
        builder.Property(li => li.Gross).HasPrecision(18, 2);
        builder.Property(li => li.Fee).HasPrecision(18, 2);
        builder.Property(li => li.Net).HasPrecision(18, 2);

        // PaymentRun FK configured from PaymentRunConfiguration.

        builder.HasOne(li => li.Timesheet)
            .WithMany(t => t.LineItems)
            .HasForeignKey(li => li.TimesheetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(li => li.Clinician)
            .WithMany()
            .HasForeignKey(li => li.ClinicianId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(li => li.PaymentRunId);
    }
}
