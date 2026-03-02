using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace PastryManager.Infrastructure.Services.Kafka;

public interface IKafkaConsumer
{
    Task StartConsumingAsync<T>(string topic, Func<T, Task<bool>> messageHandler, CancellationToken cancellationToken);
}

public class KafkaConsumer : IKafkaConsumer, IDisposable
{
    private readonly ILogger<KafkaConsumer> _logger;
    private readonly KafkaSettings _settings;
    private readonly IKafkaProducer _deadLetterProducer;
    private IConsumer<string, string>? _consumer;

    public KafkaConsumer(
        IOptions<KafkaSettings> settings,
        ILogger<KafkaConsumer> logger,
        IKafkaProducer deadLetterProducer)
    {
        _settings = settings.Value;
        _logger = logger;
        _deadLetterProducer = deadLetterProducer;
    }

    public async Task StartConsumingAsync<T>(string topic, Func<T, Task<bool>> messageHandler, CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _settings.ConsumerGroupId,
            SecurityProtocol = Enum.Parse<SecurityProtocol>(_settings.SecurityProtocol),
            SaslMechanism = Enum.Parse<SaslMechanism>(_settings.SaslMechanism),
            SaslUsername = _settings.SaslUsername,
            SaslPassword = _settings.SaslPassword,
            
            // TLS/mTLS Configuration
            SslCaLocation = _settings.SslCaLocation,
            SslCertificateLocation = _settings.SslCertificateLocation,
            SslKeyLocation = _settings.SslKeyLocation,
            SslKeyPassword = _settings.SslKeyPassword,
            
            // Consumer settings for reliability
            AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_settings.AutoOffsetReset),
            EnableAutoCommit = _settings.EnableAutoCommit,
            
            // Exactly-once semantics
            IsolationLevel = IsolationLevel.ReadCommitted,
            
            // Session and heartbeat
            SessionTimeoutMs = 30000,
            HeartbeatIntervalMs = 10000,
            
            // Security
            SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https
        };

        _consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) =>
            {
                _logger.LogError("Kafka consumer error: {Reason}", error.Reason);
            })
            .SetPartitionsAssignedHandler((_, partitions) =>
            {
                _logger.LogInformation("Partitions assigned: {Partitions}", 
                    string.Join(", ", partitions.Select(p => p.Partition.Value)));
            })
            .Build();

        _consumer.Subscribe(topic);
        _logger.LogInformation("Started consuming from topic: {Topic}", topic);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(cancellationToken);
                    
                    if (consumeResult?.Message == null)
                        continue;

                    _logger.LogInformation(
                        "Received message from {Topic}, Partition: {Partition}, Offset: {Offset}",
                        consumeResult.Topic,
                        consumeResult.Partition.Value,
                        consumeResult.Offset.Value);

                    var message = JsonSerializer.Deserialize<T>(consumeResult.Message.Value);
                    
                    if (message == null)
                    {
                        _logger.LogWarning("Failed to deserialize message, sending to dead letter queue");
                        await SendToDeadLetterQueueAsync(consumeResult, "Deserialization failed", cancellationToken);
                        _consumer.Commit(consumeResult);
                        continue;
                    }

                    // Process message with handler
                    var success = await messageHandler(message);

                    if (success)
                    {
                        // Manual commit for exactly-once semantics
                        _consumer.Commit(consumeResult);
                        _logger.LogInformation("Successfully processed and committed message");
                    }
                    else
                    {
                        _logger.LogWarning("Message processing failed, sending to dead letter queue");
                        await SendToDeadLetterQueueAsync(consumeResult, "Handler returned false", cancellationToken);
                        _consumer.Commit(consumeResult);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message from Kafka");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing message");
                }
            }
        }
        finally
        {
            _consumer.Close();
        }
    }

    private async Task SendToDeadLetterQueueAsync(ConsumeResult<string, string> consumeResult, string reason, CancellationToken cancellationToken)
    {
        try
        {
            var headers = new Headers();
            foreach (var header in consumeResult.Message.Headers)
            {
                headers.Add(header.Key, header.GetValueBytes());
            }
            headers.Add("original-topic", System.Text.Encoding.UTF8.GetBytes(consumeResult.Topic));
            headers.Add("failure-reason", System.Text.Encoding.UTF8.GetBytes(reason));
            headers.Add("failure-timestamp", System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o")));

            await _deadLetterProducer.ProduceWithHeadersAsync(
                _settings.DeadLetterTopic,
                consumeResult.Message.Key,
                consumeResult.Message.Value,
                headers,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to dead letter queue");
        }
    }

    public void Dispose()
    {
        _consumer?.Close();
        _consumer?.Dispose();
    }
}
