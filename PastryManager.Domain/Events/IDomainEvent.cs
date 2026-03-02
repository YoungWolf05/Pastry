namespace PastryManager.Domain.Events;

/// <summary>
/// Base interface for all domain events in event sourcing
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string EventType { get; }
    int Version { get; }
}
