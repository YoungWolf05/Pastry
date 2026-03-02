var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL database with fixed port
var postgres = builder.AddPostgres("postgres")
    .WithImageTag("18")
    .WithDataVolume()
    .WithPgAdmin()
    .WithHostPort(5433);

var pastryDb = postgres.AddDatabase("pastrydb");

// Add Kafka for event-driven architecture (local development)
var kafka = builder.AddKafka("kafka")
    .WithDataVolume();

// Add Kafka UI for monitoring
kafka.WithKafkaUI(c => c.WithHostPort(8081));

// Add LocalStack for S3 storage (local development)
var localstack = builder.AddContainer("localstack", "localstack/localstack", "latest")
    .WithHttpEndpoint(port: 4566, targetPort: 4566, name: "s3")
    .WithEnvironment("SERVICES", "s3,kms,secretsmanager")
    .WithEnvironment("DEBUG", "1")
    .WithEnvironment("DATA_DIR", "/tmp/localstack/data")
    .WithEnvironment("DEFAULT_REGION", "us-east-1")
    .WithEnvironment("EAGER_SERVICE_LOADING", "1")
    .WithBindMount("./localstack-init", "/etc/localstack/init/ready.d")
    .WithLifetime(ContainerLifetime.Persistent);

// Add API service with database reference and LocalStack endpoint configuration
var apiService = builder.AddProject<Projects.PastryManager_Api>("pastrymanager-api")
    .WithReference(pastryDb)
    .WithReference(kafka)  // This automatically injects the correct Kafka connection string
    .WaitFor(pastryDb)
    .WaitFor(kafka)
    .WaitFor(localstack)
    .WithEnvironment("AWS__ServiceURL", "http://localhost:4566")
    .WithEnvironment("Kafka__SecurityProtocol", "Plaintext") // No auth for local dev
    .WithEnvironment("Encryption__KmsKeyId", "alias/dev-key"); // LocalStack KMS

builder.Build().Run();
