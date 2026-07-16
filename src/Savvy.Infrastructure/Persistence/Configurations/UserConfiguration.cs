using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Savvy.Domain.Entities;

namespace Savvy.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.PublicId).IsRequired();
        builder.HasIndex(u => u.PublicId).IsUnique();

        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);

        // Role / Practice relationships are configured from the principal side
        // (RoleConfiguration / PracticeConfiguration).

        builder.HasMany(u => u.AssignedShifts)
            .WithOne(s => s.Clinician!)
            .HasForeignKey(s => s.ClinicianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
