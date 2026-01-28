using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PastryManager.Domain.Entities;

namespace PastryManager.Infrastructure.Data.Configurations;

public class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.ToTable("TaskComments");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content)
            .IsRequired()
            .HasMaxLength(1000);

        builder.HasIndex(c => c.TaskRequestId);
        builder.HasIndex(c => c.UserId);

        // Soft delete filter
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
