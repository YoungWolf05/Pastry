namespace PastryManager.Domain.Entities;

/// <summary>
/// Refresh token for JWT authentication
/// </summary>
public class RefreshToken : BaseEntity
{
    public required Guid UserId { get; init; }
    public required string Token { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }
    public required string CreatedByIp { get; init; }
    public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;
    
    // Navigation
    public User? User { get; set; }
}
