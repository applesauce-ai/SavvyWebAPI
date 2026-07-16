using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Savvy.Domain.Entities;

namespace Savvy.Infrastructure.Persistence.Configurations;

public class PracticeConfiguration : IEntityTypeConfiguration<Practice>
{
    public void Configure(EntityTypeBuilder<Practice> builder)
    {
        builder.ToTable("Practices");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);

        builder.HasMany(p => p.Users)
            .WithOne(u => u.Practice!)
            .HasForeignKey(u => u.PracticeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Shifts)
            .WithOne(s => s.Practice)
            .HasForeignKey(s => s.PracticeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.PaymentRuns)
            .WithOne(pr => pr.Practice)
            .HasForeignKey(pr => pr.PracticeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
