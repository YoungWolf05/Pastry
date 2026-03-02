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
        
        var eventStoreEntry = new EventStore
        {
            Id = Guid.NewGuid(),
            AggregateId = GetAggregateId(domainEvent),
            AggregateType = GetAggregateType(domainEvent),
            EventType = domainEvent.EventType,
            EventData = eventData,
            Version = domainEvent.Version,
            Timestamp = domainEvent.OccurredAt,
            UserId = GetUserId(domainEvent),
            CorrelationId = Guid.NewGuid().ToString(),
            Hash = hash
        };

        _context.EventStores.Add(eventStoreEntry);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Event {EventType} appended to event store for aggregate {AggregateId}",
            domainEvent.EventType,
            eventStoreEntry.AggregateId);
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
                "Event {EventType} published to Kafka topic {Topic}",
                domainEvent.EventType,
                topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} to Kafka", domainEvent.EventType);
            throw;
        }
    }

    private static string ComputeHash(string data)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }

    private static Guid GetAggregateId(IDomainEvent domainEvent)
    {
        return domainEvent switch
        {
            AccountEvent accountEvent => accountEvent.AccountId,
            TransactionEvent transactionEvent => transactionEvent.TransactionId,
            _ => throw new ArgumentException($"Unknown event type: {domainEvent.GetType().Name}")
        };
    }

    private static string GetAggregateType(IDomainEvent domainEvent)
    {
        return domainEvent switch
        {
            AccountEvent => "Account",
            TransactionEvent => "Transaction",
            _ => throw new ArgumentException($"Unknown event type: {domainEvent.GetType().Name}")
        };
    }

    private static string GetUserId(IDomainEvent domainEvent)
    {
        return domainEvent switch
        {
            AccountEvent accountEvent => accountEvent.UserId.ToString(),
            TransactionEvent => "System", // Transactions may be system-initiated
            _ => "Unknown"
        };
    }
}
