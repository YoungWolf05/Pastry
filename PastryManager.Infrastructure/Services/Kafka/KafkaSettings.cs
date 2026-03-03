namespace PastryManager.Infrastructure.Services.Kafka;

public class KafkaSettings
{
    public required string BootstrapServers { get; set; }
    public required string SecurityProtocol { get; set; } = "SaslSsl"; // SASL_SSL for mTLS
    public string SaslMechanism { get; set; } = "SCRAM-SHA-512"; // Not required for Plaintext
    public string? SaslUsername { get; set; } // Not required for Plaintext
    public string? SaslPassword { get; set; } // Not required for Plaintext
    
    // TLS/SSL Settings
    public string? SslCaLocation { get; set; }
    public string? SslCertificateLocation { get; set; }
    public string? SslKeyLocation { get; set; }
    public string? SslKeyPassword { get; set; }
    
    // Consumer Settings
    public required string ConsumerGroupId { get; set; }
    public string AutoOffsetReset { get; set; } = "earliest";
    public bool EnableAutoCommit { get; set; } = false; // Manual commit for exactly-once semantics
    
    // Producer Settings
    public string Acks { get; set; } = "all"; // Wait for all replicas
    public int MessageTimeoutMs { get; set; } = 30000;
    public int RequestTimeoutMs { get; set; } = 30000;
    public bool EnableIdempotence { get; set; } = true;
    
    // Topic Settings - default to well-known names defined in KafkaTopics
    public string AccountEventsTopic    { get; set; } = KafkaTopics.AccountEvents;
    public string TransactionEventsTopic{ get; set; } = KafkaTopics.TransactionEvents;
    public string AuditLogTopic         { get; set; } = KafkaTopics.AuditLogs;
    public string DeadLetterTopic       { get; set; } = KafkaTopics.DeadLetterQueue;
    public string TransferSagaTopic     { get; set; } = KafkaTopics.TransferSagaEvents;
}
