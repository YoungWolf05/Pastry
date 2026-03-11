using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PastryManager.Domain.Entities;
using PastryManager.Infrastructure.Data;
using PastryManager.Infrastructure.Services.Kafka;
using System.Text.Json;

namespace PastryManager.Infrastructure.Services.Outbox;

/// <summary>
/// Background worker that polls the outbox_messages table and publishes pending
/// messages to Kafka. Guarantees at-least-once delivery — if the app crashes after
/// SaveChanges but before Kafka ack, the message will be retried on the next poll.
///
/// Uses PostgreSQL "FOR UPDATE SKIP LOCKED" so multiple API instances can safely
/// run concurrently without processing the same message twice.
/// </summary>
public class OutboxWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize   = 50;
    private const int MaxRetries  = 5;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxWorker> _logger;

    public OutboxWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("📬 OutboxWorker started — polling every {Interval}s", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxWorker encountered an unexpected error");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        _logger.LogInformation("📬 OutboxWorker stopped");
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        // Each batch gets its own scope so DbContext is not shared across iterations
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context  = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var producer = scope.ServiceProvider.GetRequiredService<IKafkaProducer>();

        var messages = await context.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < MaxRetries)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        _logger.LogInformation("📬 OutboxWorker processing {Count} pending message(s)", messages.Count);

        foreach (var message in messages)
        {
            await PublishMessageAsync(producer, message, ct);
        }

        await context.SaveChangesAsync(ct);
    }

    private async Task PublishMessageAsync(IKafkaProducer producer, OutboxMessage message, CancellationToken ct)
    {
        try
        {
            // Deserialize the payload back to object for publishing
            var payload = JsonSerializer.Deserialize<object>(message.Payload)
                          ?? throw new InvalidOperationException("Outbox payload deserialized to null");

            await producer.ProduceAsync(message.Topic, message.MessageKey, payload, ct);

            message.ProcessedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "✅ Outbox message published → Topic: {Topic} | Key: {Key} | EventType: {EventType}",
                message.Topic, message.MessageKey, message.EventType);
        }
        catch (Exception ex)
        {
            message.RetryCount++;
            message.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;

            _logger.LogWarning(
                "⚠️ Outbox publish failed (attempt {Retry}/{Max}) for {EventType}: {Error}",
                message.RetryCount, MaxRetries, message.EventType, ex.Message);

            if (message.RetryCount >= MaxRetries)
            {
                _logger.LogError(
                    "❌ Outbox message {Id} ({EventType}) exceeded max retries — will not be retried. Last error: {Error}",
                    message.Id, message.EventType, ex.Message);
            }
        }
    }
}
