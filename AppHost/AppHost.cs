var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL database with fixed port
var postgres = builder.AddPostgres("postgres")
    .WithImageTag("18")
    .WithDataVolume()
    .WithPgAdmin()
    .WithHostPort(5433);

var pastryDb = postgres.AddDatabase("pastrydb");

// Add API service with database reference and wait for DB to be ready
var apiService = builder.AddProject<Projects.PastryManager_Api>("pastrymanager-api")
    .WithReference(pastryDb)
    .WaitFor(pastryDb);

builder.Build().Run();
