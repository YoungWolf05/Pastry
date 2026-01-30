using Microsoft.Extensions.DependencyInjection;

namespace PastryManager.MCP.McpServer;

/// <summary>
/// Dependency injection extensions for MCP Server
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddMcpTools(this IServiceCollection services)
    {
        // Register tool classes for DI
        services.AddSingleton<UserManagementTools>();
        services.AddSingleton<TaskManagementTools>();
        
        return services;
    }
}
