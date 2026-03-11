using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PastryManager.Domain.Entities;

namespace PastryManager.Infrastructure.Data.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Topic).IsRequired().HasMaxLength(200);
        builder.Property(x => x.MessageKey).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(2000);

        // Primary query: fetch all unprocessed messages ordered by creation time
        builder.HasIndex(x => new { x.ProcessedAt, x.CreatedAt })
               .HasDatabaseName("ix_outbox_messages_unprocessed");

        // Supports retry logic filtering on RetryCount
        builder.HasIndex(x => x.RetryCount)
               .HasDatabaseName("ix_outbox_messages_retry_count");
    }
}
