using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace PastryManager.Infrastructure.Services.Kafka;

public interface IKafkaProducer
{
    Task ProduceAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default);
    Task ProduceWithHeadersAsync<T>(string topic, string key, T message, Headers headers, CancellationToken cancellationToken = default);
}

public class KafkaProducer : IKafkaProducer, IDisposable
{
    private IProducer<string, string>? _producer;
    private readonly ILogger<KafkaProducer> _logger;
    private readonly KafkaSettings _settings;
    private readonly object _lock = new();
    private bool _initialized = false;

    public KafkaProducer(IOptions<KafkaSettings> settings, ILogger<KafkaProducer> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    private IProducer<string, string> GetProducer()
    {
        if (_initialized && _producer != null)
            return _producer;

        lock (_lock)
        {
            if (_initialized && _producer != null)
                return _producer;

            try
            {
                var config = new ProducerConfig
                {
                    BootstrapServers = _settings.BootstrapServers,
                    SecurityProtocol = Enum.Parse<SecurityProtocol>(_settings.SecurityProtocol),
                    
                    // Producer reliability settings
                    Acks = Enum.Parse<Acks>(_settings.Acks),
                    MessageTimeoutMs = _settings.MessageTimeoutMs,
                    RequestTimeoutMs = _settings.RequestTimeoutMs,
                    EnableIdempotence = _settings.EnableIdempotence,
                    
                    // Retry configuration
                    MessageSendMaxRetries = 3,
                    RetryBackoffMs = 1000,
                    
                    // Compression for efficiency
                    CompressionType = CompressionType.Snappy,
                    
                    // Security
                    SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https
                };

                // Only add SASL settings if not Plaintext
                if (_settings.SecurityProtocol != "Plaintext")
                {
                    config.SaslMechanism = Enum.Parse<SaslMechanism>(_settings.SaslMechanism);
                    config.SaslUsername = _settings.SaslUsername;
                    config.SaslPassword = _settings.SaslPassword;
                    config.SslCaLocation = _settings.SslCaLocation;
                    config.SslCertificateLocation = _settings.SslCertificateLocation;
                    config.SslKeyLocation = _settings.SslKeyLocation;
                    config.SslKeyPassword = _settings.SslKeyPassword;
                }

                _producer = new ProducerBuilder<string, string>(config)
                    .SetErrorHandler((_, error) =>
                    {
                        _logger.LogWarning("Kafka producer error: {Reason}", error.Reason);
                    })
                    .Build();

                _initialized = true;
                _logger.LogInformation("Kafka producer initialized: {BootstrapServers}", _settings.BootstrapServers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Kafka producer. Messages will be logged only.");
                _initialized = true; // Mark as initialized to avoid retry loops
            }

            return _producer!;
        }
    }

    public async Task ProduceAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
    {
        await ProduceWithHeadersAsync(topic, key, message, new Headers(), cancellationToken);
    }

    public async Task ProduceWithHeadersAsync<T>(string topic, string key, T message, Headers headers, CancellationToken cancellationToken = default)
    {
        try
        {
            var producer = GetProducer();
            if (producer == null)
            {
                _logger.LogWarning("Kafka producer not available. Message will not be published: {Topic}/{Key}", topic, key);
                return;
            }

            var serializedMessage = JsonSerializer.Serialize(message);
            
            // Add standard headers
            headers.Add("message-type", System.Text.Encoding.UTF8.GetBytes(typeof(T).Name));
            headers.Add("timestamp", System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o")));
            headers.Add("correlation-id", System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));

            var kafkaMessage = new Message<string, string>
            {
                Key = key,
                Value = serializedMessage,
                Headers = headers,
                Timestamp = new Timestamp(DateTime.UtcNow)
            };

            var deliveryResult = await producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
            
            _logger.LogInformation(
                "Message delivered to {Topic}, Partition: {Partition}, Offset: {Offset}",
                deliveryResult.Topic,
                deliveryResult.Partition.Value,
                deliveryResult.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to produce message to topic {Topic}, Key: {Key}", topic, key);
            // Don't throw - allow application to continue even if Kafka is down
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error producing message to {Topic}", topic);
        }
    }

    public void Dispose()
    {
        try
        {
            _producer?.Flush(TimeSpan.FromSeconds(10));
            _producer?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing Kafka producer");
        }
    }
}
