namespace PastryManager.Domain.Entities;

/// <summary>
/// Transactional Outbox pattern — guarantees at-least-once Kafka delivery.
/// Written to the same DB transaction as the business entity, then published
/// to Kafka by a background worker. Eliminates dual-write inconsistency.
/// </summary>
public class OutboxMessage
{
    public Guid   Id         { get; init; } = Guid.NewGuid();
    public string EventType  { get; init; } = string.Empty;   // e.g. "AccountCreatedEvent"
    public string Topic      { get; init; } = string.Empty;   // Kafka topic
    public string MessageKey { get; init; } = string.Empty;   // Kafka partition key (aggregate ID)
    public string Payload    { get; init; } = string.Empty;   // JSON serialised event

    public DateTime  CreatedAt   { get; init; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set;  }                // null = pending
    public int       RetryCount  { get; set;  } = 0;
    public string?   Error       { get; set;  }                // last failure reason
}
