var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL database
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var pastryDb = postgres.AddDatabase("pastrydb");

// Add API service with database reference and wait for DB to be ready
var apiService = builder.AddProject<Projects.PastryManager_Api>("pastrymanager-api")
    .WithReference(pastryDb)
    .WaitFor(pastryDb);

builder.Build().Run();
