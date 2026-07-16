using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Savvy.Domain.Entities;

namespace Savvy.Infrastructure.Persistence.Configurations;

public class PaymentRunConfiguration : IEntityTypeConfiguration<PaymentRun>
{
    public void Configure(EntityTypeBuilder<PaymentRun> builder)
    {
        builder.ToTable("PaymentRuns");
        builder.HasKey(pr => pr.Id);

        builder.Property(pr => pr.PublicId).IsRequired();
        builder.HasIndex(pr => pr.PublicId).IsUnique();

        builder.Property(pr => pr.PeriodStartUtc).IsRequired();
        builder.Property(pr => pr.PeriodEndUtc).IsRequired();

        builder.Property(pr => pr.FeePercentage).HasPrecision(9, 4);
        builder.Property(pr => pr.FixedFeePerTimesheet).HasPrecision(18, 2);

        builder.Property(pr => pr.BusinessReference).IsRequired().HasMaxLength(100);
        builder.HasIndex(pr => pr.BusinessReference).IsUnique();

        builder.Property(pr => pr.Currency).IsRequired().HasMaxLength(3).IsFixedLength();

        builder.Property(pr => pr.GrossTotal).HasPrecision(18, 2);
        builder.Property(pr => pr.FeeTotal).HasPrecision(18, 2);
        builder.Property(pr => pr.NetTotal).HasPrecision(18, 2);

        builder.Property(pr => pr.CreatedAtUtc).IsRequired();

        // Practice FK configured from PracticeConfiguration.

        builder.HasMany(pr => pr.LineItems)
            .WithOne(li => li.PaymentRun)
            .HasForeignKey(li => li.PaymentRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
