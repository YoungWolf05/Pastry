using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PastryManager.Domain.Entities;

namespace PastryManager.Infrastructure.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        
        builder.HasKey(t => t.Id);
        
        builder.Property(t => t.Amount)
            .IsRequired()
            .HasPrecision(18, 2);
        
        builder.Property(t => t.Currency)
            .IsRequired()
            .HasMaxLength(3);
        
        builder.Property(t => t.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.HasIndex(t => t.IdempotencyKey)
            .IsUnique();
        
        builder.Property(t => t.Reference)
            .HasMaxLength(100);
        
        builder.Property(t => t.Description)
            .HasMaxLength(500);
        
        builder.Property(t => t.ExchangeRate)
            .HasPrecision(18, 6);
        
        builder.Property(t => t.Fee)
            .HasPrecision(18, 2);
        
        builder.Property(t => t.RiskScore)
            .HasPrecision(5, 2);
        
        // Optimistic concurrency control using PostgreSQL's xmin system column
        builder.UseXminAsConcurrencyToken();
        
        // Relationships
        builder.HasOne(t => t.FromAccount)
            .WithMany(a => a.TransactionsFrom)
            .HasForeignKey(t => t.FromAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasOne(t => t.ToAccount)
            .WithMany(a => a.TransactionsTo)
            .HasForeignKey(t => t.ToAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasOne(t => t.ReversalOfTransaction)
            .WithMany(t => t.Reversals)
            .HasForeignKey(t => t.ReversalOfTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Indexes for performance
        builder.HasIndex(t => t.InitiatedAt);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => new { t.FromAccountId, t.InitiatedAt });
        builder.HasIndex(t => new { t.ToAccountId, t.InitiatedAt });
        
        // Soft delete filter
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
