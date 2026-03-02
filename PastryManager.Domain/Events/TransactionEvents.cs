using PastryManager.Domain.Enums;

namespace PastryManager.Domain.Events;

public abstract class TransactionEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType { get; init; } = string.Empty;
    public int Version { get; init; } = 1;
    public Guid TransactionId { get; init; }
}

public sealed class TransactionInitiatedEvent : TransactionEvent
{
    public TransactionInitiatedEvent()
    {
        EventType = nameof(TransactionInitiatedEvent);
    }
    
    public required Guid FromAccountId { get; init; }
    public Guid? ToAccountId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required TransactionType TransactionType { get; init; }
    public required string IdempotencyKey { get; init; }
    public string? Reference { get; init; }
    public string? Description { get; init; }
}

public sealed class TransactionProcessingEvent : TransactionEvent
{
    public TransactionProcessingEvent()
    {
        EventType = nameof(TransactionProcessingEvent);
    }
    
    public required string ProcessedBy { get; init; }
}

public sealed class TransactionCompletedEvent : TransactionEvent
{
    public TransactionCompletedEvent()
    {
        EventType = nameof(TransactionCompletedEvent);
    }
    
    public required decimal PreviousBalance { get; init; }
    public required decimal NewBalance { get; init; }
    public required string CompletedBy { get; init; }
}

public sealed class TransactionFailedEvent : TransactionEvent
{
    public TransactionFailedEvent()
    {
        EventType = nameof(TransactionFailedEvent);
    }
    
    public required string FailureReason { get; init; }
    public required string ErrorCode { get; init; }
}

public sealed class TransactionReversedEvent : TransactionEvent
{
    public TransactionReversedEvent()
    {
        EventType = nameof(TransactionReversedEvent);
    }
    
    public required Guid OriginalTransactionId { get; init; }
    public required string ReversalReason { get; init; }
    public required string ReversedBy { get; init; }
}
