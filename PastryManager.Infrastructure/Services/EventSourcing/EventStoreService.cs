using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using PastryManager.Domain.Entities;
using PastryManager.Domain.Events;
using PastryManager.Infrastructure.Data;
using PastryManager.Infrastructure.Services.Kafka;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PastryManager.Infrastructure.Services.EventSourcing;

public interface IEventStoreService
{
    Task AppendEventAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent;
    Task<IEnumerable<EventStore>> GetEventsForAggregateAsync(Guid aggregateId, string aggregateType, CancellationToken cancellationToken = default);

    // Kept for saga/test use-cases that explicitly need to push to Kafka directly
    Task PublishEventToKafkaAsync<T>(T domainEvent, string topic, CancellationToken cancellationToken = default) where T : IDomainEvent;
}

public class EventStoreService : IEventStoreService
{
    private readonly ApplicationDbContext _context;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ILogger<EventStoreService> _logger;
    private readonly KafkaSettings _kafkaSettings;

    public EventStoreService(
        ApplicationDbContext context,
        IKafkaProducer kafkaProducer,
        ILogger<EventStoreService> logger,
        IOptions<KafkaSettings> kafkaSettings)
    {
        _context = context;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
        _kafkaSettings = kafkaSettings.Value;
    }

    public async Task AppendEventAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent
    {
        var eventData = JsonSerializer.Serialize(domainEvent);
        var hash = ComputeHash(eventData);
        var aggregateId = GetAggregateId(domainEvent);
        var aggregateType = GetAggregateType(domainEvent);
        var topic = ResolveKafkaTopic(domainEvent);

        // Auto-increment version from DB — the event store owns versioning, not the domain event.
        // This prevents IX_EventStore_AggregateId_Version violations when a saga appends
        // multiple events for the same aggregate (e.g. Initiated → Completed/Failed).
        var maxVersion = await _context.EventStores
            .Where(e => e.AggregateId == aggregateId && e.AggregateType == aggregateType)
            .MaxAsync(e => (int?)e.Version, cancellationToken) ?? 0;

        var nextVersion = maxVersion + 1;

        var eventStoreEntry = new EventStore
        {
            Id            = Guid.NewGuid(),
            AggregateId   = aggregateId,
            AggregateType = aggregateType,
            EventType     = domainEvent.EventType,
            EventData     = eventData,
            Version       = nextVersion,
            Timestamp     = domainEvent.OccurredAt,
            UserId        = GetUserId(domainEvent),
            CorrelationId = Guid.NewGuid().ToString(),
            Hash          = hash
        };

        // Transactional Outbox: write event store entry AND outbox message
        // in the same SaveChanges call — atomically committed or both rolled back.
        var outboxMessage = new OutboxMessage
        {
            EventType  = domainEvent.EventType,
            Topic      = topic,
            MessageKey = aggregateId.ToString(),
            Payload    = eventData
        };

        _context.EventStores.Add(eventStoreEntry);
        _context.OutboxMessages.Add(outboxMessage);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Event {EventType} appended to event store and outbox for aggregate {AggregateId}",
            domainEvent.EventType, aggregateId);
    }

    public async Task<IEnumerable<EventStore>> GetEventsForAggregateAsync(
        Guid aggregateId,
        string aggregateType,
        CancellationToken cancellationToken = default)
    {
        return await _context.EventStores
            .Where(e => e.AggregateId == aggregateId && e.AggregateType == aggregateType)
            .OrderBy(e => e.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task PublishEventToKafkaAsync<T>(T domainEvent, string topic, CancellationToken cancellationToken = default)
        where T : IDomainEvent
    {
        try
        {
            var key = GetAggregateId(domainEvent).ToString();
            await _kafkaProducer.ProduceAsync(topic, key, domainEvent, cancellationToken);

            _logger.LogInformation(
                "Event {EventType} published directly to Kafka topic {Topic}",
                domainEvent.EventType, topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} to Kafka", domainEvent.EventType);
            // Don't rethrow — Kafka being down should not fail the business operation
        }
    }

    /// <summary>Routes domain events to the correct Kafka topic</summary>
    private static string ResolveKafkaTopic(IDomainEvent domainEvent) => domainEvent switch
    {
        AccountEvent     => KafkaTopics.AccountEvents,
        TransactionEvent => KafkaTopics.TransactionEvents,
        _                => KafkaTopics.AuditLogs
    };

    private static string ComputeHash(string data)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }

    private static Guid GetAggregateId(IDomainEvent domainEvent) => domainEvent switch
    {
        AccountEvent accountEvent         => accountEvent.AccountId,
        TransactionEvent transactionEvent => transactionEvent.TransactionId,
        _ => throw new ArgumentException($"Unknown event type: {domainEvent.GetType().Name}")
    };

    private static string GetAggregateType(IDomainEvent domainEvent) => domainEvent switch
    {
        AccountEvent     => "Account",
        TransactionEvent => "Transaction",
        _ => throw new ArgumentException($"Unknown event type: {domainEvent.GetType().Name}")
    };

    private static string GetUserId(IDomainEvent domainEvent) => domainEvent switch
    {
        AccountEvent accountEvent => accountEvent.UserId.ToString(),
        TransactionEvent          => "System",
        _                         => "Unknown"
    };
}
