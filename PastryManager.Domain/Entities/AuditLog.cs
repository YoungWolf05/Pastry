namespace PastryManager.Domain.Entities;

/// <summary>
/// Immutable audit log for compliance and forensics
/// </summary>
public class AuditLog
{
    public Guid Id { get; init; }
    public required DateTime Timestamp { get; init; }
    
    public required string UserId { get; init; }
    public required string Action { get; init; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    
    public required string IpAddress { get; init; }
    public string? UserAgent { get; init; }
    
    /// <summary>
    /// Additional metadata in JSON format
    /// </summary>
    public string? Metadata { get; init; }
    
    /// <summary>
    /// Hash of the audit log entry to detect tampering
    /// </summary>
    public required string Hash { get; set; }
}
