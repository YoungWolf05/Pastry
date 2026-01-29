using PastryManager.Application;
using PastryManager.Infrastructure;
using PastryManager.Api.Extensions;
using Serilog;

// Configure Serilog
SerilogExtensions.ConfigureSerilog();

try
{
    Log.Information("Starting PastryManager API application");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Host.UseSerilog();

    // Add services
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApiServices();

    var app = builder.Build();

    // Configure pipeline
    app.ConfigurePipeline();

    // Auto-migrate database in development
    await app.Services.MigrateDatabaseAsync(app.Environment);

    app.Run();
    Log.Information("PastryManager API stopped cleanly");
}
catch (Exception ex)
{
    Log.Fatal(ex, "PastryManager API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
