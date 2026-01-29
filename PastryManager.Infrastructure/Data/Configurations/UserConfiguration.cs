using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PastryManager.Domain.Entities;

namespace PastryManager.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(u => u.IsActive)
            .IsRequired();

        builder.HasMany(u => u.AssignedTasks)
            .WithOne(t => t.AssignedToUser)
            .HasForeignKey(t => t.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.CreatedTasks)
            .WithOne(t => t.CreatedByUser)
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Soft delete filter
        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
