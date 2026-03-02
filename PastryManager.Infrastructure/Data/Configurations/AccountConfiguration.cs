using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PastryManager.Domain.Entities;

namespace PastryManager.Infrastructure.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        
        builder.HasKey(a => a.Id);
        
        // Encrypted fields need larger storage (base64 encoded ciphertext)
        builder.Property(a => a.AccountNumber)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.HasIndex(a => a.AccountNumber)
            .IsUnique();
        
        builder.Property(a => a.IBAN)
            .HasMaxLength(500);
        
        builder.Property(a => a.SwiftCode)
            .HasMaxLength(500);
        
        builder.Property(a => a.Currency)
            .IsRequired()
            .HasMaxLength(3);
        
        builder.Property(a => a.Balance)
            .HasPrecision(18, 2);
        
        builder.Property(a => a.AvailableBalance)
            .HasPrecision(18, 2);
        
        builder.Property(a => a.DailyTransferLimit)
            .HasPrecision(18, 2);
        
        builder.Property(a => a.MonthlyTransferLimit)
            .HasPrecision(18, 2);
        
        // Optimistic concurrency control using PostgreSQL's xmin system column
        builder.UseXminAsConcurrencyToken();
        
        // Relationships
        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false); // Make optional to allow account creation without loading User navigation
        
        builder.HasMany(a => a.TransactionsFrom)
            .WithOne(t => t.FromAccount)
            .HasForeignKey(t => t.FromAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasMany(a => a.TransactionsTo)
            .WithOne(t => t.ToAccount)
            .HasForeignKey(t => t.ToAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Soft delete filter
        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
