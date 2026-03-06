namespace PastryManager.Application.Common.Interfaces;

/// <summary>
/// Abstracts event publishing so Application handlers remain decoupled from Kafka.
/// The Infrastructure layer provides the implementation via KafkaProducer.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(string topic, string key, object payload, CancellationToken cancellationToken = default);
}
