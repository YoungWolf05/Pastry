using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PastryManager.Application;
using PastryManager.Infrastructure;
using PastryManager.MCP.McpServer;

var builder = Host.CreateApplicationBuilder(args);

// Explicitly add configuration files from the application's directory
var appDirectory = AppContext.BaseDirectory;
builder.Configuration
    .SetBasePath(appDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Add logging to stderr (required for MCP stdio protocol)
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Add Application and Infrastructure layers (CQRS + Repositories)
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Add MCP tool classes
builder.Services.AddMcpTools();

// Configure MCP Server with stdio transport and tools
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<UserManagementTools>()
    .WithTools<TaskManagementTools>();

await builder.Build().RunAsync();
