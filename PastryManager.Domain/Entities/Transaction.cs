using PastryManager.Domain.Enums;

namespace PastryManager.Domain.Entities;

/// <summary>
/// Financial transaction with idempotency support and audit trail
/// </summary>
public class Transaction : BaseEntity
{
    public required Guid FromAccountId { get; set; }
    public Guid? ToAccountId { get; set; }
    
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    
    public required TransactionType TransactionType { get; set; }
    public required TransactionStatus Status { get; set; }
    
    /// <summary>
    /// Idempotency key to prevent duplicate transactions
    /// </summary>
    public required string IdempotencyKey { get; set; }
    
    public string? Reference { get; set; }
    public string? Description { get; set; }
    
    public decimal? ExchangeRate { get; set; }
    public decimal? Fee { get; set; }
    
    public DateTime InitiatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public string? FailureReason { get; set; }
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// For reversal/cancellation tracking
    /// </summary>
    public Guid? ReversalOfTransactionId { get; set; }
    
    /// <summary>
    /// IP address of the client initiating transaction
    /// </summary>
    public string? ClientIpAddress { get; set; }
    
    /// <summary>
    /// Device fingerprint for fraud detection
    /// </summary>
    public string? DeviceFingerprint { get; set; }
    
    /// <summary>
    /// Risk score calculated by fraud detection system
    /// </summary>
    public decimal? RiskScore { get; set; }
    
    // Navigation properties
    public Account? FromAccount { get; set; }
    public Account? ToAccount { get; set; }
    public Transaction? ReversalOfTransaction { get; set; }
    public ICollection<Transaction> Reversals { get; set; } = new List<Transaction>();
}
