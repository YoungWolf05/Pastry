namespace PastryManager.Domain.Entities;

/// <summary>
/// Event store for event sourcing - immutable record of all domain events
/// </summary>
public class EventStore
{
    public Guid Id { get; init; }
    public required Guid AggregateId { get; init; }
    public required string AggregateType { get; init; }
    public required string EventType { get; init; }
    public required string EventData { get; init; }
    public required int Version { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string UserId { get; init; }
    
    /// <summary>
    /// Correlation ID for tracking related events across services
    /// </summary>
    public string? CorrelationId { get; init; }
    
    /// <summary>
    /// Hash of event data to ensure integrity
    /// </summary>
    public required string Hash { get; init; }
}
