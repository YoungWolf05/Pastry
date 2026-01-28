using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PastryManager.Domain.Entities;

namespace PastryManager.Infrastructure.Data.Configurations;

public class TaskRequestConfiguration : IEntityTypeConfiguration<TaskRequest>
{
    public void Configure(EntityTypeBuilder<TaskRequest> builder)
    {
        builder.ToTable("TaskRequests");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(t => t.Priority)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.AssignedToUserId);
        builder.HasIndex(t => t.CreatedByUserId);
        builder.HasIndex(t => t.DueDate);

        builder.HasMany(t => t.Comments)
            .WithOne(c => c.TaskRequest)
            .HasForeignKey(c => c.TaskRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Soft delete filter
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
