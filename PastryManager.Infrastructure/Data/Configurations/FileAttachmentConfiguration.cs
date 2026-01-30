using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PastryManager.Domain.Entities;
using PastryManager.Domain.Enums;

namespace PastryManager.Infrastructure.Data.Configurations;

public class FileAttachmentConfiguration : IEntityTypeConfiguration<FileAttachment>
{
    public void Configure(EntityTypeBuilder<FileAttachment> builder)
    {
        builder.ToTable("FileAttachments");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(f => f.S3Key)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(f => f.S3Key)
            .IsUnique();

        builder.Property(f => f.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.FileSizeBytes)
            .IsRequired();

        builder.Property(f => f.EntityType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(f => f.EntityId)
            .IsRequired();

        builder.HasIndex(f => new { f.EntityType, f.EntityId });

        builder.Property(f => f.UploadedBy)
            .IsRequired();

        builder.HasOne(f => f.UploadedByUser)
            .WithMany()
            .HasForeignKey(f => f.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(f => f.CreatedAt)
            .IsRequired();

        builder.Property(f => f.UpdatedAt);

        builder.Property(f => f.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasQueryFilter(f => !f.IsDeleted);
    }
}
