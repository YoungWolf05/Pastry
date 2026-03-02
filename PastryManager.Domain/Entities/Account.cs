using PastryManager.Domain.Enums;

namespace PastryManager.Domain.Entities;

/// <summary>
/// Bank account entity with encrypted PII fields
/// </summary>
public class Account : BaseEntity
{
    public required Guid UserId { get; set; }
    
    /// <summary>
    /// Encrypted account number - must be encrypted at rest
    /// </summary>
    public required string AccountNumber { get; set; }
    
    public required AccountType AccountType { get; set; }
    public required AccountStatus Status { get; set; }
    public required string Currency { get; set; }
    
    public decimal Balance { get; set; }
    public decimal AvailableBalance { get; set; }
    
    /// <summary>
    /// Encrypted IBAN - must be encrypted at rest
    /// </summary>
    public string? IBAN { get; set; }
    
    /// <summary>
    /// Encrypted SWIFT/BIC code
    /// </summary>
    public string? SwiftCode { get; set; }
    
    public decimal DailyTransferLimit { get; set; }
    public decimal MonthlyTransferLimit { get; set; }
    
    public DateTime OpenedDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    
    public DateTime LastTransactionDate { get; set; }
    
    // Navigation properties
    public User? User { get; set; }
    public ICollection<Transaction> TransactionsFrom { get; set; } = new List<Transaction>();
    public ICollection<Transaction> TransactionsTo { get; set; } = new List<Transaction>();
}
