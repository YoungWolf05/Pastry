var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.PastryManager_Api>("pastrymanager-api");

builder.Build().Run();
