using PastryManager.Domain.Enums;

namespace PastryManager.Domain.Events;

public abstract class AccountEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType { get; init; } = string.Empty;
    public int Version { get; init; } = 1;
    public Guid AccountId { get; init; }
    public Guid UserId { get; init; }
}

public sealed class AccountCreatedEvent : AccountEvent
{
    public AccountCreatedEvent()
    {
        EventType = nameof(AccountCreatedEvent);
    }
    
    public required string AccountNumber { get; init; }
    public required AccountType AccountType { get; init; }
    public required string Currency { get; init; }
    public decimal InitialBalance { get; init; }
}

public sealed class AccountActivatedEvent : AccountEvent
{
    public AccountActivatedEvent()
    {
        EventType = nameof(AccountActivatedEvent);
    }
    
    public required string ActivatedBy { get; init; }
}

public sealed class AccountSuspendedEvent : AccountEvent
{
    public AccountSuspendedEvent()
    {
        EventType = nameof(AccountSuspendedEvent);
    }
    
    public required string Reason { get; init; }
    public required string SuspendedBy { get; init; }
}

public sealed class AccountFrozenEvent : AccountEvent
{
    public AccountFrozenEvent()
    {
        EventType = nameof(AccountFrozenEvent);
    }
    
    public required string Reason { get; init; }
    public required string FrozenBy { get; init; }
}

public sealed class AccountClosedEvent : AccountEvent
{
    public AccountClosedEvent()
    {
        EventType = nameof(AccountClosedEvent);
    }
    
    public required string Reason { get; init; }
    public decimal FinalBalance { get; init; }
}
