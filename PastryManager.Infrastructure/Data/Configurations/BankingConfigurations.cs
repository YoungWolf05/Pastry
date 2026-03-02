using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PastryManager.Domain.Entities;

namespace PastryManager.Infrastructure.Data.Configurations;

public class EventStoreConfiguration : IEntityTypeConfiguration<EventStore>
{
    public void Configure(EntityTypeBuilder<EventStore> builder)
    {
        builder.ToTable("EventStore");
        
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.AggregateType)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(e => e.EventData)
            .IsRequired();
        
        builder.Property(e => e.UserId)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(e => e.Hash)
            .IsRequired()
            .HasMaxLength(100);
        
        // Indexes for event sourcing queries
        builder.HasIndex(e => new { e.AggregateId, e.Version })
            .IsUnique();
        
        builder.HasIndex(e => e.Timestamp);
        builder.HasIndex(e => e.EventType);
        builder.HasIndex(e => e.CorrelationId);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        
        builder.HasKey(a => a.Id);
        
        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(a => a.EntityId)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(a => a.IpAddress)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(a => a.Hash)
            .IsRequired()
            .HasMaxLength(100);
        
        // Indexes for audit queries
        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.Action);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        
        builder.HasKey(r => r.Id);
        
        builder.Property(r => r.Token)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.HasIndex(r => r.Token)
            .IsUnique();
        
        builder.Property(r => r.CreatedByIp)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(r => r.RevokedByIp)
            .HasMaxLength(50);
        
        // Soft delete query filter to match User entity
        builder.HasQueryFilter(r => !r.IsDeleted);
        
        // Indexes
        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.ExpiresAt);
    }
}
