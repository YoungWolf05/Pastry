using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PastryManager.Application.Common.Interfaces;
using System.Text.Json;

namespace PastryManager.Infrastructure.Services.Kafka;

/// <summary>
/// Well-known Kafka topic names used across the banking system
/// </summary>
public static class KafkaTopics
{
    public const string AccountEvents      = "account-events";
    public const string TransactionEvents  = "transaction-events";
    public const string AuditLogs          = "audit-logs";
    public const string TransferSagaEvents = "transfer-saga-events";
    public const string DeadLetterQueue    = "dead-letter-queue";

    public static readonly IReadOnlyList<string> All = new[]
    {
        AccountEvents, TransactionEvents, AuditLogs, TransferSagaEvents, DeadLetterQueue
    };
}

public interface IKafkaProducer
{
    Task ProduceAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default);
    Task ProduceWithHeadersAsync<T>(string topic, string key, T message, Headers headers, CancellationToken cancellationToken = default);
}

public class KafkaProducer : IKafkaProducer, IEventPublisher, IAsyncDisposable
{
    private IProducer<string, string>? _producer;
    private readonly ILogger<KafkaProducer> _logger;
    private readonly KafkaSettings _settings;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized = false;

    public KafkaProducer(IOptions<KafkaSettings> settings, ILogger<KafkaProducer> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    private async Task<IProducer<string, string>?> GetProducerAsync()
    {
        if (_initialized) return _producer;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return _producer;

            _logger.LogInformation("Initializing Kafka producer → {Servers}", _settings.BootstrapServers);

            // Ensure topics exist before producing
            await EnsureTopicsExistAsync();

            var config = new ProducerConfig
            {
                BootstrapServers      = _settings.BootstrapServers,
                SecurityProtocol      = Enum.Parse<SecurityProtocol>(_settings.SecurityProtocol, ignoreCase: true),
                Acks                  = Acks.All,           // always wait for all replicas
                MessageTimeoutMs      = _settings.MessageTimeoutMs,
                RequestTimeoutMs      = _settings.RequestTimeoutMs,
                EnableIdempotence     = _settings.EnableIdempotence,
                MessageSendMaxRetries = 3,
                RetryBackoffMs        = 1000,
                CompressionType       = CompressionType.None, // Snappy requires native libs on Windows
                ClientId              = "banking-api-producer"
            };

            if (!string.Equals(_settings.SecurityProtocol, "Plaintext", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(_settings.SaslMechanism))
                    config.SaslMechanism = Enum.Parse<SaslMechanism>(_settings.SaslMechanism, ignoreCase: true);
                config.SaslUsername           = _settings.SaslUsername;
                config.SaslPassword           = _settings.SaslPassword;
                config.SslCaLocation          = _settings.SslCaLocation;
                config.SslCertificateLocation = _settings.SslCertificateLocation;
                config.SslKeyLocation         = _settings.SslKeyLocation;
                config.SslKeyPassword         = _settings.SslKeyPassword;
            }

            _producer = new ProducerBuilder<string, string>(config)
                .SetErrorHandler((_, e) => _logger.LogWarning("Kafka producer error: {Reason}", e.Reason))
                .Build();

            _initialized = true;
            _logger.LogInformation("✅ Kafka producer ready. Topics: {Topics}", string.Join(", ", KafkaTopics.All));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Kafka producer initialization failed - events will be skipped");
            _initialized = true; // prevent retry loop
        }
        finally
        {
            _initLock.Release();
        }

        return _producer;
    }

    private async Task EnsureTopicsExistAsync()
    {
        try
        {
            var adminConfig = new AdminClientConfig
            {
                BootstrapServers = _settings.BootstrapServers
            };

            using var admin = new AdminClientBuilder(adminConfig).Build();

            var specs = KafkaTopics.All.Select(t => new TopicSpecification
            {
                Name              = t,
                NumPartitions     = 3,
                ReplicationFactor = 1,
                Configs           = new Dictionary<string, string>
                {
                    ["retention.ms"]    = "604800000",  // 7 days
                    ["cleanup.policy"] = "delete"
                }
            }).ToList();

            await admin.CreateTopicsAsync(specs);
            _logger.LogInformation("✅ Kafka topics created: {Topics}", string.Join(", ", KafkaTopics.All));
        }
        catch (CreateTopicsException ex)
        {
            var failed = ex.Results.Where(r => r.Error.Code != ErrorCode.TopicAlreadyExists).ToList();
            if (failed.Count == 0)
                _logger.LogInformation("✅ Kafka topics already exist");
            else
                _logger.LogWarning("Some topics could not be created: {Errors}",
                    string.Join(", ", failed.Select(e => $"{e.Topic}: {e.Error.Reason}")));
        }
    }

    public async Task ProduceAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
    {
        await ProduceWithHeadersAsync(topic, key, message, new Headers(), cancellationToken);
    }

    // IEventPublisher — allows Application layer handlers to publish without referencing Confluent.Kafka
    Task IEventPublisher.PublishAsync(string topic, string key, object payload, CancellationToken cancellationToken)
        => ProduceAsync(topic, key, payload, cancellationToken);

    public async Task ProduceWithHeadersAsync<T>(string topic, string key, T message, Headers headers, CancellationToken cancellationToken = default)
    {
        var producer = await GetProducerAsync();
        if (producer == null)
        {
            _logger.LogWarning("⚠️ Kafka unavailable - skipping publish to {Topic}/{Key}", topic, key);
            return;
        }

        try
        {
            var serialized = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            headers.Add("message-type",  System.Text.Encoding.UTF8.GetBytes(typeof(T).Name));
            headers.Add("timestamp",     System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o")));
            headers.Add("correlation-id",System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
            headers.Add("source",        System.Text.Encoding.UTF8.GetBytes("banking-api"));

            var result = await producer.ProduceAsync(topic, new Message<string, string>
            {
                Key       = key,
                Value     = serialized,
                Headers   = headers,
                Timestamp = new Timestamp(DateTime.UtcNow)
            }, cancellationToken);

            _logger.LogInformation(
                "✅ Event published → Topic: {Topic} | Key: {Key} | Partition: {Partition} | Offset: {Offset}",
                result.Topic, key, result.Partition.Value, result.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "❌ Failed to publish to {Topic}: {Error}", topic, ex.Error.Reason);
            // Send to dead letter queue without rethrowing
            await SendToDeadLetterAsync(topic, key, message, ex.Error.Reason, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error publishing to {Topic}", topic);
        }
    }

    private async Task SendToDeadLetterAsync<T>(string originalTopic, string key, T message, string reason, CancellationToken ct)
    {
        if (_producer == null) return;
        try
        {
            var dlq = JsonSerializer.Serialize(new
            {
                originalTopic,
                key,
                message,
                errorReason = reason,
                failedAt    = DateTimeOffset.UtcNow
            });
            await _producer.ProduceAsync(KafkaTopics.DeadLetterQueue,
                new Message<string, string> { Key = key, Value = dlq }, ct);
            _logger.LogWarning("⚠️ Message sent to dead-letter-queue: {Topic}/{Key}", originalTopic, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write to dead-letter-queue");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { _producer?.Flush(TimeSpan.FromSeconds(10)); } catch { /* best effort */ }
        _producer?.Dispose();
        _initLock.Dispose();
        await ValueTask.CompletedTask;
    }
}
